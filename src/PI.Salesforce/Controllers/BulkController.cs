using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;
using Services;

namespace Controllers;

[Route("/salesforce/v1/[controller]")]
public class BulkController : APIController
{
    private readonly ILogger<BulkController> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public BulkController(
        ILogger<BulkController> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    [Authorize("default")]
    [HttpPost("/salesforce/v1/[controller]/Filter")]
    public async Task<FilterResponse> FilterObjectsAsync([FromBody] FilterRequest request)
    {
        using var scope = _logger.AddScope(new
        {
            request.ObjectType,
            Filter = JsonConvert.SerializeObject(request.Conditions),
            Context.OrganizationId,
        });

        _logger.LogInformation("Filter Objects");

        if (!Context.OrganizationId.HasValue) throw new ForbiddenException("Only Zee users should be able to");
        
        var objectType = await _objectTypeService.GetAsync(Context, "sf_" + request.ObjectType);
        if (objectType == null) throw new NotFoundException($"{request.ObjectType} not found");
        
        // TODO: ensure context should have access to read 
        // ...

        var filter = request.Conditions[0];
        var ids = filter.Value as object[];

        var query = await _connection.Filter<SalesforceCustomObject>(objectType.CollectionName, objectType.DatabaseName)
            .Eq(x => x.AccountId, Context.AccountId)
            .In(x=>x.EntityId, new []
            {
                Context.AccountId, 
                Context.OrganizationId
            })
            .In(x => x.Properties[filter.FieldName], ids)
            .FindAsync();
        
        // TODO: make sure the user should have access to the results
        // should it check that the leads are for the right branch?
        // ... 
        
        _logger.LogInformation("Found {ObjectsCount} for filter", query.Count);
        
        return new FilterResponse
        {
            ObjectType = request.ObjectType,
            Objects = query.Select(x=>x.Properties),
            Success = true,
        };
    }

    [Authorize("default")]
    [HttpPost("/salesforce/v1/[controller]/{objectTypeName}")]
    public async Task<BulkPostResponse> ImportMaterialAssignmentLineItemsAsync([FromRoute] string objectTypeName, [FromBody] BulkPostRequest request)
    {
        using var scope = _logger.AddScope(new
        {
            request.ObjectType,
            ObjectsCount = request.Objects?.Length,
            Context.OrganizationId,
        });

        _logger.LogInformation("Bulk Upsert Objects");
        
        if (!Context.OrganizationId.HasValue) throw new ForbiddenException("Only Zee users should be able to");
        
        var objectType = await _objectTypeService.GetAsync(Context, "sf_" + objectTypeName);
        if (objectType == null) throw new NotFoundException($"{objectTypeName} not found");

        // TODO: check settings to figure out if the user can update and/or insert 
        // ...

        var success = new List<ObjectUpdated>();
        foreach (var obj in request.Objects)
        {
            if (!obj.TryGetValue("MobileGUID__c", out var mobileObj) || mobileObj is not string mobileGuid)
            {
                // no id? skip
                continue;
            }

            string externalId;
            if (obj.TryGetValue("Id", out var idObj))
            {
                _logger.LogInformation("Update existing {ObjectType}: {Id} for {MobileGUID}", objectTypeName, idObj, mobileGuid);
                externalId = idObj.ToString();
            }
            else
            {
                _logger.LogInformation("Insert {ObjectType} for {MobileGUID}", objectTypeName, mobileGuid);
                externalId = mobileGuid[7..].Replace("-", string.Empty);
                obj["Id"] = externalId;
            }

            var id = await UpdateObjectAsync(Context, objectType, externalId, obj);
            if (id == null) continue;

            success.Add(new ObjectUpdated
            {
                Id = externalId,
                MobileGuid = mobileGuid,
            });
        }

        return new BulkPostResponse
        {
            ObjectType = objectTypeName,
            Success = request.Objects.Length == success.Count,
            Objects = success,
        };
    }

    private async Task<SalesforceCustomObject> UpdateObjectAsync(IEntityContext context, ObjectType objectType, string externalId, Dictionary<string, object> properties)
    {
        using var scope = _logger.AddScope(new
        {
            ObjectType = objectType.Name,
            ExternalId = externalId,
        });

        // TODO: use config to decide about Name and Description
        // ...

        // TODO: process IsDeleted ?
        // ...

        var now = DateTime.UtcNow;
        var id = Model.NewObjectId();

        var query = _connection.Filter<SalesforceCustomObject>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.ExternalId, externalId)
                .Update
                .SetOnInsert(x => x.ObjectType, objectType.Name)
                .SetOnInsert(x => x.ObjectTypeId, objectType.Id)
                .SetOnInsert(x => x.Id, id)
                .SetOnInsert(x => x.AccountId, context.AccountId)
                .SetOnInsert(x => x.EntityId, context.OrganizationId)
                .SetOnInsert(x => x.CreatedOn, now)
                .SetOnInsert(x => x.ExternalId, externalId)
                .Set(x => x.LastModifiedOn, now)
                .Set(x => x.Properties, properties)
                .Set(x => x.LastActor, context.Actor())
            ;

        if (objectType.InitialFlowId.HasValue) query.SetOnInsert(x => x.FlowId, objectType.InitialFlowId);
        if (objectType.InitialObjectStatusId.HasValue) query.SetOnInsert(x => x.ObjectStatusId, objectType.InitialObjectStatusId);

        var record = await query.UpdateAndGetOneAsync(true);
        if (record == null)
        {
            _logger.LogError("Failed to upsert");
        }
        else
        {
            _logger.LogInformation("Upserted record: {Id}", record.Id);
        }

        // TODO: fire event (compare id to know if is new or update?)
        // ...

        return record;
    }
}

public class BulkPostRequest
{
    public string ObjectType { get; set; }
    public Dictionary<string, object>[] Objects { get; set; }
}

public class FilterRequest
{
    public string ObjectType { get; set; }
    public Condition[] Conditions { get; set; }
}

public class FilterResponse
{
    public string ObjectType { get; set; }
    public string Message { get; set; }
    public bool Success { get; set; }
    public IEnumerable<object> Objects { get; set; }
}

public class BulkPostResponse
{
    public string ObjectType { get; set; }
    public string Message { get; set; }
    public bool Success { get; set; }
    public IEnumerable<ObjectUpdated> Objects { get; set; }
}

public class ObjectUpdated
{
    public string Id { get; set; }

    public string MobileGuid { get; set; }
}