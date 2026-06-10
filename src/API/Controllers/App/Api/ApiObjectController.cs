using System.Dynamic;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using PI.Shared.Attributes;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Authorize("rest")]
// [ApiExplorerSettings(IgnoreApi = true)]
[ApiExplorerSettings(GroupName = "rest")]
[Route("/app/api/Object")]
public class ApiObjectController : APIController
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public ApiObjectController(MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    /// <summary>
    /// Get all "readable" properties
    /// - 304 if not modified since
    /// </summary>
    [HttpGet("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})")]
    [UseApiNames]
    public async Task<Dictionary<string, object>> GetObjectAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);

        var ifModifiedSince = Request.Headers.IfModifiedSince.FirstOrDefault();
        if (
            objectType.Fields.ContainsKey(nameof(Model.LastModifiedOn)) &&
            ifModifiedSince != null &&
            DateTime.TryParse(ifModifiedSince, out var lastModifiedDate)
        )
        {
            var record = await _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(Model.IdFieldName, objectId.AsSerializedId())
                .Lte(nameof(Model.LastModifiedOn), lastModifiedDate)
                .IncludeField(Model.IdFieldName)
                .FirstOrDefaultAsync();

            if (record != null) throw new NotModifiedException(objectTypeName, objectId);
        }

        var flatObject = await _objectTypeService.GetFlatObjectAsync(Context, objectType, objectId);
        if (flatObject == null) throw new NotFoundException($"{objectTypeName} not found");
        return flatObject;
    }

    /// <summary>
    /// Add object to list of recents for the user
    /// - does not return anything
    /// - right now does not check whether the user should have access to it or not to the object 
    /// </summary>
    /// <param name="objectTypeName"></param>
    /// <param name="objectId"></param>
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})/Recent")]
    [UseApiNames]
    public async Task AddRecentObjectAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException($"{objectTypeName} not found");
        if (!objectType.CanRead(Context)) throw new ForbiddenException(Context);
        
        await _objectTypeService.AddRecentObjectAsync(Context, objectType, objectId);
    }
    
    /// <summary>
    /// Get dataview (like /api/v1/CustomObject) but serializing without changing the case
    /// </summary>
    [HttpPost("/app/api/Object/{objectTypeName}/DataView")]
    [HttpPost("/app/api/Object({objectTypeName:regex(^[[a-z_0-9\\.]]+$)})/DataView")]
    [Produces("text/csv", "application/json")]
    [UseApiNames]
    public async Task<DataViewResponse> DataViewAsync([FromRoute] string objectTypeName, [FromBody] DataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException();

        builder.UseApiNames = true;
        builder.IncludeHiddenFields = false;
        // builder.AutoGenerateReferenceFieldNames = false;
        builder.SkipCustomizations = true;

        return await builder.BuildDataViewAsync(Context, objectType, request);
    }

    /// <summary>
    /// Get recents data view (using custom pipeline)
    /// </summary>
    [HttpPost("/app/api/Object/{objectTypeName}/Recent/DataView")]
    [HttpPost("/app/api/Object({objectTypeName:regex(^[[a-z_0-9\\.]]+$)})/Recent/DataView")]
    [UseApiNames]
    public async Task<DataViewResponse> RecentsResultSetAsync([FromRoute] string objectTypeName, [FromBody] FilterRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        if (!Context.UserId.HasValue) throw new ForbiddenException(Context, "No user");

        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException();

        var dataViewRequest = new DataViewRequest
        {
            Criteria = request.Criteria,
            Fields = request.Fields,
            View = request.View,
            GroupedFields = request.GroupedFields,
        };
        
        builder.UseApiNames = true;
        builder.IncludeHiddenFields = false;
        builder.SkipCustomizations = true;
        builder.LimitToRecents = true;

        var dataView = await builder.BuildDataViewAsync(Context, objectType, dataViewRequest);
        
        return dataView;
    }

    /// <summary>
    /// First implementation of recent (not relying on pipeline)
    /// </summary>
    private async Task<DataViewResponse> _GetRecentsResultSetAsync([FromRoute] string objectTypeName, [FromBody] FilterRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        if (!Context.UserId.HasValue) throw new ForbiddenException(Context, "No user");

        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException();

        // get recent 
        var recents = await GetRecentObjectsAsync(objectTypeName, request);

        var dataViewRequest = new DataViewRequest
        {
            Criteria = (request.Criteria ?? Enumerable.Empty<Condition>())
                .Where(x => x.FieldName != Model.IdFieldName)
                .Append(Condition.In(Model.IdFieldName, recents.Select(x => x.ObjectId)))
                .ToArray(),
            Fields = request.Fields,
            View = request.View,
            GroupedFields = request.GroupedFields,
        };
        
        builder.UseApiNames = true;
        builder.IncludeHiddenFields = false;
        builder.SkipCustomizations = true;
        
        var dataView = await builder.BuildDataViewAsync(Context, objectType, dataViewRequest);

        dataView.Result = SortResults(recents, dataView.Result.OfType<ExpandoObject>());

        return dataView;
    }
    
    /// <summary>
    /// Filter (e.g. DataView)
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/Filter")]
    [UseApiNames]
    public async Task<List<ExpandoObject>> DataViewAsync([FromRoute] string objectTypeName, [FromBody] FilterRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException();

        var dataViewRequest = new DataViewRequest
        {
            Criteria = request.Criteria,
            Top = request.Top > 0 && request.Top < 100 ? request.Top : 0,
            Skip = request.Skip > 0 ? request.Skip : 0,
            Fields = request.Fields,
            OrderBy = !string.IsNullOrEmpty(request.OrderBy) && request.ReverseOrder ? $"-{request.OrderBy}" : request.OrderBy,
            View = request.View,
            // ContentType = headers.FirstOrDefault();
        };

        return await BuildResultSetAsync(builder, objectType, dataViewRequest);
    }

    /// <summary>
    /// Get Recent Objects (before applying filter)
    /// - assumes the id is either a GUID (as string) or an ObjectId
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}/Recent")]
    [UseApiNames]
    public async Task<IEnumerable<ExpandoObject>> RecentsDataViewAsync([FromRoute] string objectTypeName, [FromBody] FilterRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        if (!Context.UserId.HasValue) throw new ForbiddenException(Context, "No user");

        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException();

        // get recent 
        var recents = await GetRecentObjectsAsync(objectTypeName, request);

        var dataViewRequest = new DataViewRequest
        {
            Criteria = (request.Criteria ?? Enumerable.Empty<Condition>())
                .Where(x => x.FieldName != Model.IdFieldName)
                .Append(Condition.In(Model.IdFieldName, recents.Select(x => x.ObjectId)))
                .ToArray(),
            Fields = request.Fields,
            View = request.View,
            GroupedFields = request.GroupedFields,
        };

        if (recents.IsEmpty())
        {
            return Enumerable.Empty<ExpandoObject>();
        }

        var result = await BuildResultSetAsync(builder, objectType, dataViewRequest);

        return SortResults(recents, result);
    }

    /// <summary>
    /// Create Object and return it 
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}")]
    [UseApiNames]
    public async Task<DataFormActionResponse> CrateObjectAsync([FromRoute] string objectTypeName, [FromBody] ExpandoObject body)
    {
        var getObjOptions = new GetObjectOptions
        {
            UseFieldApiNames = true,
        };

        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName, getObjOptions);
        objectType = await _objectTypeService.ResolveSubTypeForUserInputAsync(Context, objectType, body, getObjOptions);
        if (!objectType.CanCreate(Context)) throw new ForbiddenException(Context, FormAction.Add);

        var addOptions = new ObjectTypeService.AddObjectOptions
        {
            UseFieldApiNames = true,
        };

        var result = await _objectTypeService.AddObjectAsync(Context, objectType, body, addOptions);
        if (!result)
        {
            throw new BadRequestException(result.Status);
        }

        // var flatObject = await _objectTypeService.RecursivelyFlattenAsync(Context, objectType, result.Value.Object);

        return new DataFormActionResponse
        {
            Success = result.IsSuccess,
            Ids = result.Value?.ObjectId != null
                ?
                [
                    result.Value.ObjectId,
                ]
                : [],
            Action = result.Value?.FiredEvent?.Action ?? "Create",
            Message = result.Status ?? result.Value?.FiredEvent?.Description,
            NextUrl = null,
            RunId = result.Value?.FiredEvent?.RunId,
        };
    }

    /// <summary>
    /// Update Object
    /// </summary>
    [HttpPatch("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})")]
    [UseApiNames]
    public async Task<DataFormActionResponse> UpdateObjectAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromBody] ExpandoObject body)
    {
        var getObjOptions = new GetObjectOptions
        {
            UseFieldApiNames = true,
        };

        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName, getObjOptions);
        var expando = await _objectTypeService.GetExpandoObjectByIdAsync(Context, objectType, objectId);
        var result = await _objectTypeService.UpdateObjectAsync(Context, objectType, body, objectId, expando, new ObjectTypeService.UpdateObjectOptions
        {
            UseFieldApiNames = true,
            PartialUpdate = true,
        });

        return new DataFormActionResponse
        {
            Success = result.IsSuccess,
            Ids =
            [
                objectId,
            ],
            Action = result.Value?.FiredEvent?.Action ?? "Update",
            Message = result.Status ?? result.Value?.FiredEvent?.Description,
            NextUrl = null,
            RunId = result.Value?.FiredEvent?.RunId,
        };
    }

    /// <summary>
    /// Delete Object and return deleted
    /// 404: if not found
    /// </summary>
    [HttpDelete("{objectTypeName:regex(^[[A-Za-z_0-9\\.]]+$)}({objectId})")]
    [UseApiNames]
    public async Task<Dictionary<string, object>> DeleteObjectAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId)
    {
        // TODO: add support for Request.Headers.IfUnmodifiedSince
        // ...

        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        var expandoObject = await _objectTypeService.DeleteObjectByIdAsync(Context, objectType, objectId);
        var flatObject = await _objectTypeService.RecursivelyFlattenAsync(Context, objectType, expandoObject);
        return flatObject;
    }

    /// <summary>
    /// Tags lookup
    /// </summary>
    [HttpGet("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/Tags")]
    [UseApiNames]
    public async Task<IEnumerable<AugmentedTag>> TagsLookupAsync([FromRoute] string objectTypeName, [FromQuery] string partialTag)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException("ObjectType");

        var dataViewRequest = new DataViewRequest
        {
            Criteria = !string.IsNullOrWhiteSpace(partialTag)
                ?
                [
                    Condition.Eq(Condition.AutoComplete, partialTag)
                ]
                : null,
            Top = 25,
            // Skip = 0,
            // OrderBy = 
            // ContentType = headers.FirstOrDefault();
        };

        var values = await _objectTypeService.LookupTagsAsync(Context, objectType, dataViewRequest);

        // TODO: enhance tags before returning
        // ...
        return values.Select(x => new AugmentedTag { Tag = x.Id });
    }

    /// <summary>
    /// Reference Field Lookup
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/Lookup")]
    [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/Lookup({fieldName})")]
    [UseApiNames]
    public async Task<IEnumerable<ReferenceValue>> LookupByIdAsync([FromRoute] string objectTypeName, [FromRoute] string fieldName, [FromBody] FilterRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException("ObjectType");

        var dataViewRequest = new DataViewRequest
        {
            Criteria = request.Criteria,
            Top = request.Top > 0 && request.Top < 100 ? request.Top : 0,
            LookupField = fieldName,
            OrderBy = objectType.LookupFields?.Name ?? nameof(IModel.Name),
            // Skip = request.Skip > 0 ? request.Skip : 0,
            // OrderBy = !string.IsNullOrEmpty(request.OrderBy) && request.ReverseOrder ? $"-{request.OrderBy}" : request.OrderBy,
            // Fields = request.Fields, // should always be he reference value fields
            // ContentType = headers.FirstOrDefault();
        };

        builder.UseApiNames = true;
        builder.IncludeHiddenFields = false;
        builder.AutoGenerateReferenceFieldNames = false;
        builder.SkipCustomizations = true;

        return await builder.LookupAsync(Context, objectType, dataViewRequest);
    }

    /// <summary>
    /// Top matching values for a field 
    /// </summary>
    [HttpPost("{objectTypeName:regex(^[[a-z_0-9\\.]]+$)}/Field({fieldName})/Top/Lookup")]
    [UseApiNames]
    public async Task<IEnumerable<ReferenceValue>> TopValuesForFieldAsync([FromRoute] string objectTypeName, [FromRoute] string fieldName, DataViewRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);

        // TODO: check conditions
        // if it is a "#id" (initial lookup) just return it as the only option
        // ... 

        var condition = request.Criteria?.FirstOrDefault(x => x.FieldName == Condition.AutoComplete);
        if (objectType == null) throw new NotFoundException($"{objectTypeName}: {condition?.Value}");

        request.LookupField = fieldName;

        // request.Criteria = condition?.Value!=null ? 
        // [
        //     Condition.Eq(fieldName, condition.Value)
        // ] : [];

        return await builder.TopValuesAsync(Context, objectType, request);
    }
    
    IEnumerable<ExpandoObject> SortResults(List<RecentObject> recents, IEnumerable<ExpandoObject> result)
    {
        var dict = result.ToDictionary(x => ((IDictionary<string, object>)x)[Model.IdFieldName]);
        var sortedIds = recents.Select(x => x.ObjectId).Distinct();
        foreach (var sortedId in sortedIds)
        {
            if (dict.TryGetValue(sortedId, out var value)) yield return value;
        }
    }

    private async Task<List<RecentObject>> GetRecentObjectsAsync(string objectTypeName, FilterRequest request)
    {
        var top = request.Top > 0 && request.Top < 100 ? request.Top : 25;

        var recents = await _connection.Filter<RecentObject>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .In(x => x.EntityId, Context.GetAllUserIds()) // you may still end up w/o recents having them because the dataview will filter out objects for the ghost users (for example)
            .OrBuilder(
                q => q.Eq(x => x.ObjectType, objectTypeName),
                q => q.AnyEq(x => x.AllObjectTypes, objectTypeName)
            )
            .SortDesc(x => x.LastModifiedOn)
            .Limit(top)
            .IncludeField(x => x.ObjectId)
            .FindAsync();

        return recents;
    }

    private async Task<List<ExpandoObject>> BuildResultSetAsync(ObjectDataViewBuilder builder, ObjectType objectType, DataViewRequest dataViewRequest)
    {
        // TODO: pass some parameter to tell it to not use or update the "cached settings" for the object/user
        // ...
        builder.UseApiNames = true;
        builder.IncludeHiddenFields = false;
        builder.AutoGenerateReferenceFieldNames = false;
        builder.SkipCustomizations = true;

        return await builder.BuildResultSetAsync(Context, objectType, dataViewRequest);
    }    
}

public class FilterRequest
{
    public Condition[] Criteria { get; set; } // "FilterCondition" object type 
    public int Top { get; set; }
    public int Skip { get; set; }

    /// <summary>
    /// List of (ordered) fields to be returned
    /// </summary>
    public string[] Fields { get; set; }

    public string OrderBy { get; set; }
    public bool ReverseOrder { get; set; }

    /// <summary>
    /// Optional view
    /// </summary>
    public string View { get; set; }

    /// <summary>
    /// EXPERIMENTAL (Optional)
    /// when specified will add group stage
    /// *** at least one field should be marked as "Distinct" *** 
    /// - for fields listed here, override projection for fields using
    /// - for all other Fields, will use $first 
    /// </summary>
    public Dictionary<string, GroupedFieldProjection> GroupedFields { get; set; }
}

public class AugmentedTag
{
    public string Tag { get; set; }
    // color
    // category
    // ...
}