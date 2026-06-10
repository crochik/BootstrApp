using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Services;

public class ReportService
{
    private static readonly TimeZoneInfo timeZone = TimeZoneConverter.TZConvert.GetTimeZoneInfo("America/Phoenix");
    private static DateTime TodayLocal => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    private static DateTime TodayUtc => TimeZoneInfo.ConvertTimeToUtc(TodayLocal, timeZone);

    private readonly MongoConnection _connection;

    public ReportService(MongoConnection connection)
    {
        _connection = connection;
    }

    // TODO: convert into AppDataView
    public async Task<DataViewResponse> RenderReportAsync(IEntityContext context, string name, DataViewRequest request)
    {
        var report = await _connection.Filter<AppReport>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Name, name)
            .Eq(x => x.MinRole, EntityRoleId.Account)
            .FirstOrDefaultAsync();

        if (report == null) throw new NotFoundException($"{name} report not found");

        var response = await report.GetAsync(context, _connection, request);

        response.Id = report.Id;
        // response.ObjectType = null;
        // response.ObjectId = null;

        return response;
    }

    public async Task<DataViewResponse> ReportAsync(IEntityContext context, string name, DataViewRequest request)
    {
        var report = await _connection.Filter<AppReport>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Name, name)
            .FirstOrDefaultAsync();

        return await ReportAsync(context, report, request);
    }

    public async Task<DataViewResponse> ReportAsync(IEntityContext context, Guid id, DataViewRequest request)
    {
        var report = await _connection.Filter<AppReport>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        return await ReportAsync(context, report, request);
    }

    private async Task<DataViewResponse> ReportAsync(IEntityContext context, AppReport report, DataViewRequest request)
    {
        if (report == null) return null;

        switch (context.Role)
        {
            case EntityRoleId.Admin:
            case EntityRoleId.Account:
            case EntityRoleId.Manager:
            case EntityRoleId.User:
                break;
            default:
                throw new ForbiddenException(context);
        }

        request ??= new DataViewRequest();

        // entity id
        var entityId = request.Criteria.TryGetUidValueFromEqCondition(nameof(IEntityOwnedModel.EntityId), out var e) ? e : default(Guid?);
        entityId = context.Role switch
        {
            EntityRoleId.Admin => entityId ?? context.AccountId.Value,
            EntityRoleId.Account => entityId ?? context.AccountId.Value,
            EntityRoleId.Manager => context.OrganizationId.Value,
            _ => context.UserId.Value
        };

        switch (report.MinRole)
        {
            case EntityRoleId.Admin:
                if (context.Role != EntityRoleId.Admin && context.Role != EntityRoleId.Account) throw new ForbiddenException(context);
                break;

            case EntityRoleId.Manager:
                if (context.Role != EntityRoleId.Manager && context.Role != EntityRoleId.Admin) throw new ForbiddenException(context);
                break;

            case EntityRoleId.User:
                break;

            default:
                throw new ForbiddenException(context);
        }

        if (report.MinRole != EntityRoleId.Admin)
        {
            // add entity id filter 
            var criteria = (request.Criteria ?? Enumerable.Empty<Condition>())
                .Where(x => x.FieldName != nameof(IEntityOwnedModel.EntityId))
                .Append(new Condition
                {
                    FieldName = nameof(IEntityOwnedModel.EntityId),
                    Value = entityId.ToString(),
                    Operator = Operator.Eq,
                });

            request.Criteria = criteria.ToArray();
        }

        var response = report.Template switch
        {
            ReportTemplate.Daily => await CalculateDailyAsync(context, report, request, entityId.Value),
            ReportTemplate.Monthly => await CalculateMonthlyAsync(context, report, request, entityId.Value),
            _ => await report.GetAsync(context, _connection, request)
        };
        
        if (report.DataView?.FilterForm != null)
        {
            foreach (var field in report.DataView.FilterForm.Fields)
            {
                if (field.Name == nameof(IEntityOwnedModel.EntityId))
                {
                    field.DefaultValue = entityId;
                }
            }
        }

        response.Id = report.Id;
        // response.ObjectType = null;
        // response.ObjectId = null;

        return response;
    }

    private async Task<DataViewResponse> CalculateMonthlyAsync(IEntityContext context, AppReport report, DataViewRequest request, Guid entityId)
    {
        var filter = request.Criteria?.FirstOrDefault(x => string.Equals(x.FieldName, "filter"))?.Value as string;
        var beginOfMonth = GetStart(0);

        var items = Enumerable.Range(0, 12).Select(c =>
        {
            var (monthStart, monthEnd) = GetMonth(c);
            var key = $"{monthStart:MM/dd/yyyy}";
            var value = $"{monthStart:MMMM/yy}";
            return new KeyValuePair<string, string>(key, value);
        }).ToArray();

        var current = items.FirstOrDefault(x => string.Equals(x.Key, filter));
        if (current.Key == null) current = items.First();

        var (start, end) = DateTime.TryParse(current.Key, out var startDate) ?
            GetMonth(startDate) :
            GetMonth(0);

        // convert to utc
        start = TimeZoneInfo.ConvertTimeToUtc(start, timeZone);
        end = TimeZoneInfo.ConvertTimeToUtc(end, timeZone);

        // TODO: validate stored procedure parameters
        // ...

        var args = new Dictionary<string, object>
        {
            { "Start", start },
            { "End", end },
            { nameof(IEntityContext.AccountId), context.AccountId.Value.ToString() },
            { nameof(IEntityContext.EntityId), entityId.ToString() },
        };

        if (request.Criteria != null)
        {
            foreach (var condition in request.Criteria.Where(x => x.Operator == Operator.Eq && x.Value != null))
            {
                args.TryAdd(condition.FieldName, condition.Value);
            }
        }

        var rows = await report.StoredProcedure.ExecuteAsync<object>(_connection, args);

        var response = new DataViewResponse
        {
            Request = request,
            View = report.DataView,
            Result = rows
        };

        AddFilter("Month", response, items, current.Key);

        return response.UpdateFields();

        // // try to use builder
        // report.DataView.FilterForm ??= new PI.Shared.Form.Models.Form
        // {
        //     Name = "Filter",
        // };
        //
        // report.DataView.FilterForm.Fields = (report.DataView.FilterForm?.Fields ?? Enumerable.Empty<FormField>()).Append(
        //     new SelectField
        //     {
        //         Name = "filter",
        //         Label = "Month",
        //         SelectFieldOptions = new SelectFieldOptionsBuilder(items),
        //         DefaultValue = current.Key,
        //     }
        // ).ToArray();
        //
        // request.Criteria = request.Criteria
        //     .Where(x => x.FieldName != "Start" && x.FieldName != "End")
        //     .Append(new Condition
        //     {
        //         FieldName = "Start",
        //         Value = start
        //     })
        //     .Append(new Condition
        //     {
        //         FieldName = "End",
        //         Value = end
        //     })
        //     .ToArray();
        //
        // var response = await report.GetAsync(context, _connection, request);
        // return response;
    }

    private static void AddFilter(string name, DataViewResponse result, KeyValuePair<string, string>[] items, string current)
    {
        result.View.FilterForm ??= new PI.Shared.Form.Models.Form
        {
            Name = $"{result.View.Name}.Filter",
        };

        result.View.FilterForm.Fields = (result.View.FilterForm?.Fields ?? Enumerable.Empty<FormField>()).Append(
            new SelectField
            {
                Name = "filter",
                Label = name,
                SelectFieldOptions = new SelectFieldOptionsBuilder(items),
                DefaultValue = current,
            }
        ).ToArray();
    }

    private DateTime GetStart(int monthsAgo)
    {
        var beginOfMonth = TodayLocal;
        beginOfMonth = new DateTime(beginOfMonth.Year, beginOfMonth.Month, 1);
        var month = beginOfMonth.Month - monthsAgo;

        return month > 12 ?
            new DateTime(beginOfMonth.Year + 1, month - 12, 1) :
            (
                month > 0 ?
                    new DateTime(beginOfMonth.Year, month, 1) :
                    new DateTime(beginOfMonth.Year - 1, month + 12, 1)
            );
    }

    private (DateTime, DateTime) GetMonth(int monthsAgo)
    {
        var start = GetStart(monthsAgo);
        var end = GetStart(monthsAgo - 1); // .Subtract(TimeSpan.FromDays(1));
        return (start, end);
    }

    private (DateTime, DateTime) GetMonth(DateTime start)
    {
        var month = start.Month + 1;
        var end = month < 13 ? new DateTime(start.Year, month, 1) : new DateTime(start.Year + 1, month - 12, 1);
        // end = end.Subtract(TimeSpan.FromDays(1));
        return (start, end);
    }

    private async Task<DataViewResponse> CalculateDailyAsync(IEntityContext context, AppReport report, DataViewRequest request, Guid entityId)
    {
        var filter = request.Criteria?.FirstOrDefault(x => string.Equals(x.FieldName, "filter"))?.Value as string;
        var items = Enumerable.Range(0, 60).Select(c =>
        {
            var day = TodayUtc.AddDays(-c);
            var value = day.ToString("MM/dd/yyyy");
            var name = day.ToShortDateString();
            return new KeyValuePair<string, string>(value, name);
        }).ToArray();

        var index = Array.FindIndex(items, x => string.Equals(x.Key, filter));
        if (index < 0) index = 0;

        var start = TodayUtc.AddDays(-index);

        // TODO: valida stored procedure parameters
        // ...

        var args = new Dictionary<string, object>
        {
            { "Start", start },
            { "End", start.AddDays(1) },
            { nameof(IEntityContext.AccountId), context.AccountId.Value.ToString() },
            { nameof(IEntityContext.EntityId), entityId.ToString() },
        };

        var rows = await report.StoredProcedure.ExecuteAsync<object>(_connection, args);

        var response = new DataViewResponse
        {
            Request = request,
            View = report.DataView,
            Result = rows
        };

        AddFilter("Date", response, items, items[index].Key);

        return response.UpdateFields();
    }
}