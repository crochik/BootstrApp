using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Mongo;
using DevExpress.DataAccess.Json;
using DevExpress.XtraReports.UI;
using DevExpress.XtraReports.Web.ClientControls;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Newtonsoft.Json;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Reports.Services;

public class BridgeService
{
    private readonly ILogger<BridgeService> _logger;
    private readonly MongoConnection _connection;

    public BridgeService(
        ILogger<BridgeService> logger,
        MongoConnection connection
    )
    {
        _logger = logger;
        _connection = connection;
    }

    /// <summary>
    /// return list of data sources (reports?) for the user
    /// </summary>
    public async Task<Dictionary<string, object>> GetAvailableDataSourcesAsync(IEntityContext context)
    {
        var dict = new Dictionary<string, object>();

        // TODO: include objects 
        // ...

        // if (context.Role == EntityRoleId.Admin)
        // {
        //     // add object types
        //     // TODO: limit by profile/role?
        //     var objectTypes = await _connection.Filter<ObjectType>()
        //         .Eq(x => x.AccountId, context.AccountId)
        //         .Eq(x => x.EntityId, context.AccountId)
        //         // .Ne(x => x.NativeType, null)
        //         .SortAsc(x => x.Name)
        //         .FindAsync();

        //     foreach (var objectType in objectTypes)
        //     {
        //         if (objectType.NativeType == null)
        //         {
        //             // custom object: have to limit by object type
        //             // query.Eq(nameof(CustomObject.ObjectTypeId), objectType.Id);
        //         }

        //         if (!objectType.CanRead(context)) continue;
        //         var appDataView = await _connection.GetAsync<AppDataView>(context, objectType.Name);
        //         if (appDataView == null) continue;

        //         var name = string.IsNullOrEmpty(objectType.Description) ? objectType.Name : $"{objectType.Name}: {objectType.Description}";
        //         var dataSource = ToDataSource(name, objectType, appDataView);
        //         if (dataSource == null) continue;

        //         dict.Add(name, dataSource);
        //     }
        // }

        var query = _connection.Filter<AppReport>()
                .Eq(x => x.AccountId, context.AccountId)
                .In(x => x.Template, new[] { ReportTemplate.None, default(ReportTemplate?) })
                .Exists(x => x.StoredProcedure.Parameters[0], false)
            ;

        // switch (context.Role)
        // {
        //     case EntityRoleId.Admin:
        // }

        var list = await query
            .SortAsc(x => x.Description)
            .FindAsync();

        foreach (var report in list)
        {
            var name = $"Report: {report.Description ?? report.Name}";
            var dataSource = ToDataSource(name, report, report);
            if (dataSource == null) continue;

            dict.TryAdd(name, dataSource);
        }

        return dict;
    }

    private JsonDataSource ToDataSource<TModel, TView>(string name, TModel model, TView dataView)
        where TModel : IModel
        where TView : IDataView
    {
        var source = new UriJsonSource(new Uri($"{model.GetType().Name}:{model.Id}"));
        source.QueryParameters.AddRange(
            new[]
            {
                new QueryParameter("Id", model.Id),
                new QueryParameter("_t", model.GetType().Name),
            }
        );

        var ds = new JsonDataSource
        {
            Name = name,
            JsonSource = source,
            Schema = new JsonSchemaNode
            {
                NodeType = JsonNodeType.Array,
                Name = "result",
            }
        };

        var okParametes = new[] { nameof(IEntityContext.AccountId), nameof(IEntityContext.EntityId), nameof(IEntityContext.OrganizationId) };
        var unknownParameters = (dataView.StoredProcedure.Parameters ?? Array.Empty<Parameter>())
            .Select(x => x.Name)
            .Except(okParametes)
            .ToArray();

        if (unknownParameters.Length > 0)
        {
            _logger.LogError("{objectType}: Unexpected {parameters}", model.GetType().Name, string.Join(", ", unknownParameters));
            // parameters
            // ...
            return null;
        }

        // fields
        var fields = dataView.DataView.Fields.Select(x => new JsonSchemaNode
        {
            NodeType = JsonNodeType.Property,
            Name = x.Name,
            DisplayName = x.Label ?? x.Name,
            Type = x switch
            {
                CheckboxField _ => typeof(bool),
                DateField _ => typeof(DateTime),
                DateTimeField _ => typeof(DateTime),
                TimeField _ => typeof(DateTime),
                NumberField _ => typeof(decimal),
                _ => typeof(object)
            }
        }).ToArray();

        ds.Schema.AddChildren(fields);

        return ds;
    }

    /// <summary>
    /// Load data to show in preview for the designer
    /// </summary>
    public async Task LoadPreviewAsync(IEntityContext context, XtraReport report)
    {
        var dataView = await GetReportDataViewAsync(context, report);
        var response = await dataView?.GetAsync(context, _connection, new DataViewRequest());
        if (response != null && report.DataSource is JsonDataSource js)
        {
            var json = JsonConvert.SerializeObject(response.Result);

            // await File.WriteAllTextAsync($"{report.Name}.json", json);
            js.JsonSource = new CustomJsonSource(json);
        }
    }

    private async Task<IDataView> GetReportDataViewAsync(IEntityContext context, XtraReport report)
    {
        if (report.DataSource is not JsonDataSource js) return null;
        if (js.JsonSource is not UriJsonSource uri) return null;
        if (uri.QueryParameters[nameof(AppReport.Id)]?.Value is not string idParamValue) return null;
        if (!Guid.TryParse(idParamValue, out var reportId)) return null;
        if (uri.QueryParameters["_t"]?.Value is not string type) return null;

        if (type == nameof(AppReport))
        {
            // Report
            return await _connection.Filter<AppReport>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, reportId)
                .FirstOrDefaultAsync();
        }

        if (type == nameof(DxReport))
        {
            // DX Report
            return await GetAsync(context, reportId);
        }

        if (type == nameof(ObjectType))
        {
            // object type
            var objectType = await _connection.Filter<ObjectType>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, reportId)
                .Eq(x => x.Namespace, null)
                .FirstOrDefaultAsync();

            if (!objectType.CanRead(context) || objectType.NativeType == null) return null;
            var appDataView = await _connection.GetProfileElementAsync<AppDataView>(context, objectType.Name);
            if (appDataView == null) return null;

            var limitByType = "{ \"$match\": { \"ObjectTypeId\": \"" + objectType.Id.AsSerializedId() + "\" } }";
            appDataView.StoredProcedure.Pipeline = appDataView.StoredProcedure.Pipeline.Prepend(limitByType).ToArray();

            // custom object: have to limit by object type
            // query.Eq(nameof(CustomObject.ObjectTypeId), objectType.Id);

            return appDataView;
        }

        return null;
    }

    public async ValueTask<XtraReport> LoadReportAsync(IEntityContext context, string url)
    {
        if (!Guid.TryParse(url, out var id)) throw new FaultException("Invalid report url");

        var report = await GetAsync(context, id);
        if (report == null)
        {
            throw new FaultException("Failed to load report");
        }

        var bytes = Encoding.Default.GetBytes(report.Layout);
        var ms = new MemoryStream(bytes);
        var xtraReport = XtraReport.FromXmlStream(ms);
        if (xtraReport.DataSource is not JsonDataSource js) throw new FaultException("Invalid Data Source");

        // recalculate schema so we can change the view without having to start over
        var freshJs = ToDataSource(report.Name, report, report);
        js.Schema = freshJs.Schema;

        // load data
        var response = await report.GetAsync(context, _connection, new DataViewRequest());
        var json = JsonConvert.SerializeObject(response.Result);
        js.JsonSource = new CustomJsonSource(json);

        return xtraReport;
    }

    /// <summary>
    /// return report (bytes) as it was serialized by a call to report.SaveLayoutXml
    /// </summary>
    public async Task<byte[]> LoadReportDataAsync(IEntityContext context, string url)
    {
        if (url == "blank")
        {
            return BlankReport();
        }

        if (!Guid.TryParse(url, out var id)) throw new FaultException("Invalid report url");

        var report = await GetAsync(context, id);
        if (report != null)
        {
            return Encoding.Default.GetBytes(report.Layout);
        }

        throw new FaultException("Report not found");
    }

    public async Task<DxReport> GetAsync(IEntityContext context, Guid id)
    {
        var report = await _connection.Filter<DxReport>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            // .Eq(x => x.EntityId, context.UserId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (report?.EntityId == context.EntityId) return report;

        return context.CanAccess(report) ? report : null;
    }

    private static byte[] BlankReport()
    {
        var blank = new XtraReport
        {
            Name = "blank",
            DisplayName = "Blank"
        };

        var ms = new MemoryStream();
        blank.SaveLayoutToXml(ms);
        return ms.ToArray();
    }

    public async Task<Dictionary<string, string>> GetListAsync(IEntityContext context)
    {
        var list = await _connection.Filter<DxReport>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.EntityId, context.UserId)
            .SortAsc(x => x.Description)
            .FindAsync();

        return list.ToDictionary(x => x.Id.ToString(), x => string.IsNullOrWhiteSpace(x.Description) ? x.Name : x.Description);
    }

    private Guid? GetReportSourceId(XtraReport report)
    {
        if (report.DataSource is not JsonDataSource ds)
        {
            return null;
        }

        if (ds.JsonSource is not UriJsonSource uri)
        {
            return null;
        }

        if (uri.QueryParameters[nameof(AppReport.Id)]?.Value is not string idParamValue)
        {
            return null;
        }

        return Guid.TryParse(idParamValue, out var reportId) ? reportId : default(Guid?);
    }

    public async Task<string> SaveAsync(IEntityContext context, XtraReport report, string url = null)
    {
        var src = await GetReportDataViewAsync(context, report);
        if (src == null) throw new FaultException("Can't find report source");

        // massage dataview? 
        // unset pagesize so we load all records
        src.DataView.PageSize = null;

        // parameters?
        // ...

        var existing = default(DxReport);
        if (Guid.TryParse(url, out var id))
        {
            // save existing
            existing = await GetAsync(context, id);
            if (existing == null) throw new FaultException("Invalid report id");
            if (existing.EntityId != context.EntityId) throw new FaultException("Access denied");
        }
        else
        {
            id = Guid.NewGuid();
        }

        if (report.DataSource is JsonDataSource ds)
        {
            var source = new UriJsonSource(new Uri($"{nameof(DxReport)}:{id}"));
            source.QueryParameters.AddRange(
                new[]
                {
                    new QueryParameter(nameof(DxReport.Id), id),
                    new QueryParameter("_t", nameof(DxReport)),
                }
            );

            ds.JsonSource = source;
        }

        var ms = new MemoryStream();
        report.SaveLayoutToXml(ms);
        ms.Seek(0, SeekOrigin.Begin);
        var layout = new StreamReader(ms).ReadToEnd();

        if (existing != null)
        {
            // save existing
            var result = await _connection.Filter<DxReport>()
                .Eq(x => x.Id, existing.Id)
                .Update.Set(x => x.Layout, layout)
                .UpdateOneAsync();

            if (result.MatchedCount != 1) throw new FaultException("Failed to update report");
            return existing.Id.ToString();
        }

        // create 
        var dst = new DxReport
        {
            Id = id,
            CreatedOn = DateTime.UtcNow,
            AccountId = context.AccountId.Value,
            EntityId = context.UserId.Value,
            Name = report.Name,
            Description = string.IsNullOrEmpty(report.DisplayName) ? null : report.DisplayName,
            StoredProcedure = src.StoredProcedure,
            DataView = src.DataView,
            Layout = layout,
        };

        await _connection.InsertAsync(dst);

        return dst.Id.ToString();
    }

    public async Task<string> CreateAsync(IEntityContext context, XtraReport report, string name)
    {
        report.DisplayName = name;

        return await SaveAsync(context, report);
    }
}