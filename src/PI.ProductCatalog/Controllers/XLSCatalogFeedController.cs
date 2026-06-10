using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using PI.ProductCatalog.Models;
using PI.ProductCatalog.Services;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Route("/productcatalog/v1/CatalogFeed/xls")]
public class XLSCatalogFeedController : AbstractCatalogFeedController<XLSCatalogFeed>
{
    private readonly MongoConnection _connection;

    public XLSCatalogFeedController(
        ObjectTypeService objectTypeService,
        MongoConnection connection
    ) : base(objectTypeService)
    {
        _connection = connection;
    }

    /// <summary>
    /// Old way to add XLS feed so it would add inbox ... now the inbox is created on demand on first import
    /// </summary>
    [Obsolete("use objecttypeservice directly")]
    [Authorize("managerplus")]
    [HttpPost("DataForm")]
    public async Task<DataFormActionResponse> EditFormOnActionAsync([FromBody] DataFormActionRequest request)
    {
        return request.Action switch
        {
            FormAction.Add => await AddFeedAsync(request),
            _ => await OnActionAsync(request),
        };
    }

    [Authorize("managerplus")]
    [HttpPost("DataFile")]
    [Produces("text/csv", "application/json")]
    public Task<IDataViewResponse> DataFileAsync([FromBody] DataFormActionRequest request, [FromServices] ReportService service)
    {
        if (request?.SelectedIds?.Length != 1) throw new BadRequestException("Invalid or missing Catalog Feed");
        return GetCatalogFeedDataFileAsync(service, request.SelectedIds[0]);
    }

    [Authorize("managerplus")]
    [HttpPost("/productcatalog/v1/CatalogFeed({id})/xls/DataFile")]
    [Produces("text/csv", "application/json")]
    public Task<IDataViewResponse> CatalogFeedDataFileAsync([FromRoute] Guid id, [FromBody] DataFormActionRequest request, [FromServices] ReportService service)
        => GetCatalogFeedDataFileAsync(service, id);

    private async Task<IDataViewResponse> GetCatalogFeedDataFileAsync(ReportService service, Guid id)
    {
        var catalogFeed = await _connection.Filter<CatalogFeed, XLSCatalogFeed>(Context, false)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (catalogFeed == null) throw new NotFoundException(nameof(XLSCatalogFeed), id);

        // TODO: use AppDataView instead?
        // ...
        var report = await service.RenderReportAsync(Context, $"{nameof(XLSCatalogFeed)}.DataFile", new DataViewRequest
        {
            Criteria = new[]
            {
                Condition.Eq(nameof(CatalogItem.CatalogFeedId), catalogFeed.Id.AsSerializedId()),
                Condition.Eq(nameof(CatalogItem.AccountId), Context.AccountId.AsSerializedId()),
            }
        });

        report.View.Name = catalogFeed.Name;

        return report;
    }

    [Authorize("managerplus")]
    [HttpPost("Download/DataForm")]
    public DataFormActionResponse RedirectToDownloadCatalogFeedAsync([FromBody] DataFormActionRequest request)
    {
        if (!request.TryGetGuidParam("CatalogFeedId", out var id)) throw new BadRequestException("Invalid or missing Catalog Feed");

        return new(request, "Redirecting to file...", true)
        {
            NextUrl = $"dataFile://productcatalog/v1/CatalogFeed({id})/xls"
        };
    }

    [Authorize("managerplus")]
    [HttpGet("Download/DataForm")]
    public async Task<Form> DonwloadFormASync()
    {
        var form = (await _connection.GetProfileElementAsync<AppForm>(Context, $"{nameof(XLSCatalogFeed)}|Download"))?.Form;
        if (form == null) throw new ForbiddenException(Context, "Can't upload spreadsheets");
        return form;
    }

    private async Task<DataFormActionResponse> AddFeedAsync(DataFormActionRequest request)
    {
        Guid entityId;

        switch (Context.Role)
        {
            case EntityRoleId.Admin:
                if (!request.TryGetGuidParam(nameof(XLSCatalogFeed.EntityId), out entityId))
                {
                    return new DataFormActionResponse(request, "Missing required EntityId parameter");
                }

                break;

            case EntityRoleId.Manager:
                entityId = Context.OrganizationId.Value;
                request.Parameters[nameof(XLSCatalogFeed.EntityId)] = entityId;
                break;

            default:
                throw new ForbiddenException(Context, "Can't add Catalog Feed");
        }

        if (!request.TryGetStrParam(nameof(XLSCatalogFeed.Name), out var name))
        {
            return new DataFormActionResponse(request, "Missing required Name parameter");
        }

        if (!request.TryGetGuidParam(nameof(XLSCatalogFeed.EmailInboxId), out var emailInboxId))
        {
            // Create new inbox
            var template = await _connection.Filter<EmailInbox>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Name, "ACCOUNT_TEMPLATE")
                .Eq(x => x.IsActive, false)
                .FirstOrDefaultAsync();

            if (template == null) return new DataFormActionResponse(request, "No template available for account");

            template.Id = Model.NewGuid();
            template.Name = name;
            template.Description = null;
            template.CreatedOn = DateTime.UtcNow;
            template.LastActor = Context.Actor();
            template.LastModifiedOn = null;
            template.EntityId = entityId;

            await _objectTypeService.InsertAsync(Context, template);

            request.Parameters[nameof(XLSCatalogFeed.EmailInboxId)] = template.Id;
        }

        return await OnActionAsync(request);
    }

    [Authorize("managerplus")]
    [HttpPost("Upload/DataForm")]
    [Consumes("application/octet-stream", "multipart/form-data")]
    public async Task<DataFormActionResponse> UploadFileAsync(
        IFormFile file,
        [FromForm] Guid catalogFeedId,
        [FromServices] CSVFileImporter importer)
    {
        var result = await importer.UploadFileAsync(Context, file, catalogFeedId);
        if (!result) return Error(result.Status);

        return new DataFormActionResponse
        {
            Action = "Upload",
            Success = true,
            // Message = $"{file.FileName} with {spreadsheet.RowsCount} items added",
            // NextUrl = $"dataGrid://productcatalog/v1/Spreadsheet({spreadsheet.Id})",
            // NextUrl = $"dataGrid://productcatalog/v1/Item/Staging({spreadsheet.Id})",
            // NextUrl = "dataGrid://api/v1/CustomObject/Spreadsheet", 
            NextUrl = $"dataForm://api/v1/AppDataForm/{nameof(XLSCatalogFeed)}|FileUploaded"
        };
    }

    private static DataFormActionResponse Error(string message) => new()
    {
        Action = "Upload",
        Message = message
    };

    [Authorize("managerplus")]
    [HttpGet("Upload/DataForm")]
    public async Task<Form> GetUploadFormAsync()
    {
        var form = (await _connection.GetProfileElementAsync<AppForm>(Context, $"{nameof(XLSCatalogFeed)}|Upload"))?.Form;
        if (form == null) throw new ForbiddenException(Context, "Can't upload spreadsheets");
        return form;
    }

    [Authorize("managerplus")]
    [HttpPost("EmailInbox/Lookup")]
    public async Task<IEnumerable<ReferenceValue>> EmailInboxLookupAsync(DataViewRequest request, [FromServices] MongoConnection connection)
    {
        var query = connection.Filter<EmailInbox>(Context, false);

        if (request.Criteria.TryGetUidValueFromEqCondition(Condition.LookupId, out var lookupId))
        {
            // hack for first load
            query.Eq(x => x.Id, lookupId);
        }

        if (request.Criteria.TryGetEqCondition(Condition.AutoComplete, out var condition) && !string.IsNullOrEmpty(condition.Value?.ToString()))
        {
            // autocomplete
            query.Regex(x => x.Name, new BsonRegularExpression($"{Regex.Escape(condition.Value.ToString())}", "i"));
        }

        var list = await query
            .Eq(x => x.IsActive, true)
            .IncludeField(x => x.Id)
            .IncludeField(x => x.Name)
            .FindAsync();

        return list.Select(x => new ReferenceValue
        {
            Id = x.Id.ToString(),
            Value = x.Name,
        });
    }

    [Authorize("managerplus")]
    [HttpPost("Lookup")]
    public async Task<IEnumerable<ReferenceValue>> XLSLookupAsync(DataViewRequest request, [FromServices] MongoConnection connection)
    {
        var query = connection.Filter<CatalogFeed, XLSCatalogFeed>(Context, false);

        if (request.Criteria.TryGetUidValueFromEqCondition(Condition.LookupId, out var lookupId))
        {
            // hack for first load
            query.Eq(x => x.Id, lookupId);
        }

        if (request.Criteria.TryGetEqCondition(Condition.AutoComplete, out var condition) && !string.IsNullOrEmpty(condition.Value?.ToString()))
        {
            // autocomplete
            query.Regex(x => x.Name, new BsonRegularExpression($"{Regex.Escape(condition.Value.ToString())}", "i"));
        }

        if (request.Criteria.TryGetUidValueFromEqCondition(nameof(XLSCatalogFeed.EntityId), out var entityId))
        {
            // hack for first load
            query.Eq(x => x.EntityId, entityId);
        }

        var list = await query
            .IncludeField(x => x.Id)
            .IncludeField(x => x.Name)
            .IncludeField("_t")
            // .Project(x => new ReferenceValue { Id = x.Id.ToString(), Value = x.Name })
            // .ToListAsync();
            .FindAsync();

        var result = list.Select(x => new ReferenceValue { Id = x.Id.ToString(), Value = x.Name });
        return result;
    }
}