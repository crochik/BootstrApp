using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Route("/productcatalog/v1/Item")]
public class ItemStagingController : AbstractItemController
{
    private readonly ObjectTypeService _objectTypeService;
    private readonly UserActionService _userActionService;

    public ItemStagingController(
        ILogger<ItemStagingController> logger,
        IMapper mapper,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        UserActionService userActionService
    ) : base(logger, mapper, connection)
    {
        _objectTypeService = objectTypeService;
        _userActionService = userActionService;
    }

    [Authorize("managerplus")]
    [HttpPost("Staging({parentId})/DataView")]
    [ProducesResponseType(typeof(DataViewResponse), 200)]
    [Produces("text/csv", "application/json")]
    public async Task<DataViewResponse> StagingDataViewAsync(
        [FromBody] DataViewRequest request, 
        [FromRoute] Guid parentId
    )
    {
        var entityId = EntityId;

        var spreadsheet = await _connection.Filter<Spreadsheet>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, parentId)
            .FirstOrDefaultAsync();

        if (spreadsheet == null) throw new NotFoundException(nameof(Spreadsheet), parentId);

        var response = new DataViewResponse
        {
            Request = request,
            View = GetDataView(request, true, $"productcatalog/v1/Item/Staging({parentId})"),
        };

        response.Result = await GetResultAsync<CatalogItemStaging>(entityId, response, parentId);

        // add option to see spreadsheet
        await spreadsheet.UpdateAsync(
            _connection,
            Context,
            response,
            _objectTypeService,
            new ActionMenuItem
            {
                Name = "Review Spreadsheet",
                Action = $"dataGrid://productcatalog/v1/Spreadsheet({parentId})"
            }
        );

        return response.UpdateFields();
    }

    /// <summary>
    /// Get Form triggered by User Action
    /// </summary>
    [Authorize("default")]
    [HttpGet("Staging({id})/Action({actionId})/DataForm")]
    public async Task<Form> GetActionAsync([FromRoute] Guid id, [FromRoute] Guid actionId)
    {
        var objectTypeName = nameof(Spreadsheet);
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException("ObjectType");
        
        var obj = await _objectTypeService.UnsafeGetFlatObjectByIdAsync(Context, objectType, id);
        if (obj == null) throw new NotFoundException(objectTypeName, id);

        var objectStatusId = obj.GetOptionalGuid(nameof(FlowObjectModel.ObjectStatusId));
        var action = await _objectTypeService.GetUserActionUsingObjectTypeAsync(Context, objectType, actionId, objectStatusId);
        if (action == null) throw new NotFoundException(nameof(EventType), actionId);

        var form = (action.Trigger as UserTrigger)?.Form;
        if (form == null) throw new NotFoundException("No form");

        return form;
    }

    /// <summary>
    /// Execute user action
    /// </summary>
    [HttpPost("Staging({id})/Action({actionId})/DataForm")]
    [ProducesResponseType(typeof(DataFormActionResponse), 200)]
    public async Task<IActionResult> RunActionAsync([FromRoute] Guid id, [FromRoute] Guid actionId, [FromBody] DataFormActionRequest request)
    {
        request.SelectedIds = new[] { id };
        var result = await _userActionService.ExecuteAsync(Context, nameof(Spreadsheet), actionId, request);
        return Ok(result);
    }

    [Authorize("default")]
    [HttpGet("Staging({parentId})({id})/DataForm")]
    public async Task<Form> GetStagingEditFormAsync([FromRoute] Guid id, [FromRoute] Guid parentId)
        => await GetEditFormAsync<CatalogItemStaging>(EntityId, id);

    [Authorize("default")]
    [HttpPost("Staging({parentId})/{objectType}/Lookup")]
    public async Task<IEnumerable<BreadcrumbReferenceValue>> StagingLookupAsync([FromRoute] string objectType, [FromRoute] Guid parentId, DataViewRequest request)
        => await LookupAsync<CatalogItemStaging>(EntityId, objectType, request, parentId);
}