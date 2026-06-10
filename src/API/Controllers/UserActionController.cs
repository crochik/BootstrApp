using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Authorize("default")]
[Route("/api/v1/[controller]")]
public class UserActionController : AbstractUserActionController
{
    public UserActionController(ILogger<UserActionController> logger, MongoConnection connection, UserActionService service, ObjectTypeService objectTypeService)
        : base(logger, connection, service, objectTypeService)
    {
    }

    /// <summary>
    /// Get Form triggered by User Action (potentially for multiple objects)
    /// </summary>
    [HttpGet("/api/v1/{objectTypeName}/[controller]({eventId})/DataForm")]
    [ProducesResponseType(typeof(Form), 200)]
    public Task<Form> GetActionAsync([FromRoute] string objectTypeName, [FromRoute] Guid eventId)
        => BuildActionFormForEventAsync(objectTypeName, eventId);

    /// <summary>
    /// Execute user action
    ///     * for entity object types, if selected id is missing, will infer from the context 
    /// </summary>
    [HttpPost("/api/v1/{objectTypeName}/[controller]({eventId})/DataViewAction")]
    public async Task<DataFormActionResponse> RunDataViewActionAsync([FromRoute] string objectTypeName, [FromRoute] Guid eventId, [FromBody] DataViewActionRequest request, [FromServices] ObjectDataViewBuilder builder)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException(nameof(ObjectType));

        var eventType = await _objectTypeService.GetUserActionUsingObjectTypeAsync(Context, objectType, eventId, null);

        if (eventType?.Trigger is not UserTrigger trigger)
        {
            _logger.LogError("Invalid User Action: {EventId}", eventId);
            return new DataFormActionResponse(request)
            {
                Message = $"Invalid User Action for {objectTypeName}",
            };
        }

        if (request.SelectedIds?.Length > 0)
        {
            // TODO: should it check/enforce "trigger.ImplicitSnapshot"?
            // TODO: should it do something different?
            // ...

            // regular (dataForm) action?
            return await _service.ExecuteAsync(Context, objectTypeName, eventType, request);
        }

        if (trigger.Form == null)
        {
            // can't handle missing ids when there is no form to continue
            return DataFormActionResponse.Error(request, "Missing selected Ids");
        }

        var appDataView = default(AppDataView);
        if (string.IsNullOrEmpty(request.View))
        {
            appDataView = await builder.CreateAppDataViewAsync(Context, objectType,
                new DataViewRequest
                {
                    Criteria = request.Criteria,
                    OrderBy = request.OrderBy,
                    Fields = request.Fields,
                    View = request.View,
                }
            );
        }
        else
        {
            // load current view to find id
            appDataView = await _connection.Filter<AppDataView>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Name, request.View)
                .Eq(x => x.ObjectType, objectTypeName)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();
        }

        if (appDataView == null)
        {
            return new DataFormActionResponse(request)
            {
                Message = "Failed to generate view",
            };
        }

        // TODO: count records?
        // ...

        // if (!string.IsNullOrEmpty(trigger.SnapshotObjectType))
        // {
        //     // TODO: do not create the snapshot here (wait for confirmation) 
        //     // ...
        //
        //     var user = await _connection.Filter<Entity, User>()
        //         .Eq(x => x.AccountId, Context.AccountId.Value)
        //         .Eq(x => x.Id, Context.UserId.Value)
        //         .Ne(x => x.IsActive, false)
        //         .FirstOrDefaultAsync();
        //
        //     if (user == null) throw NotFoundException.New<User>(Context.UserId.Value);
        //
        //     var snapShotId = await _service.CreateSnapshotAsync(Context, user, trigger, appDataView, request.Parameters);
        // }

        return new DataFormActionResponse(request)
        {
            // Message = $"View Saved",
            Success = true,
            NextUrl = $"dataForm://api/v1/{objectTypeName}/AppDataView({appDataView.Id})/UserAction({eventId})",
        };
    }

    /// <summary>
    /// Execute user action
    ///     * for entity object types, if selected id is missing, will infer from the context 
    /// </summary>
    [HttpPost("/api/v1/{objectTypeName}/[controller]({eventId})/DataForm")]
    public Task<DataFormActionResponse> RunActionAsync([FromRoute] string objectTypeName, [FromRoute] Guid eventId, [FromBody] DataFormActionRequest request)
        => RunUserActionAsync(objectTypeName, eventId, request);

    /// <summary>
    /// Get Form triggered by User Action for a view
    /// </summary>
    [HttpGet("/api/v1/{objectTypeName}/AppDataView({appDataViewId})/[controller]({eventId})/DataForm")]
    [ProducesResponseType(typeof(Form), 200)]
    public async Task<Form> GetViewActionAsync([FromRoute] string objectTypeName, [FromRoute] Guid appDataViewId, [FromRoute] Guid eventId)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        var action = await _objectTypeService.GetUserActionUsingObjectTypeAsync(Context, objectType, eventId, null);
        if (action == null) throw new NotFoundException("Event");

        // allow loading inactive (e.g. system)
        var appDataView = await _connection.Filter<AppDataView>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, appDataViewId)
            .Eq(x => x.ObjectType, objectTypeName)
            .FirstOrDefaultAsync();

        if (appDataView == null) throw NotFoundException.New<AppDataView>(appDataViewId);

        return await _service.BuildUserActionFormAsync(Context, action, BuildRunContext(), appDataView: appDataView);
    }

    /// <summary>
    /// Execute user action
    ///     * for entity object types, if selected id is missing, will infer from the context 
    /// </summary>
    [HttpPost("/api/v1/{objectTypeName}/AppDataView({appDataViewId})/[controller]({eventId})/DataForm")]
    public async Task<DataFormActionResponse> RunViewActionAsync([FromRoute] string objectTypeName, [FromRoute] Guid appDataViewId, [FromRoute] Guid eventId, [FromBody] DataFormActionRequest request)
    {
        if (request.SelectedIds?.Length > 0) throw new BadRequestException("View actions should not include selected ids");
        var result = await _service.ExecuteAsync(Context, objectTypeName, eventId, request);
        return result;
    }

    [HttpGet("/api/v1/{objectTypeName}({objectId})/[controller]/Menu")]
    public async Task<Menu> GetActionsMenuForObjectAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException("ObjectType");

        var (_, menuItems) = await _objectTypeService.GetUserActionsMenuItemsAsync(Context, objectType, objectId, includeMultiple: true);

        var menu = new Menu
        {
            Name = "Actions",
            Icon = nameof(Icons.Action),
            Items = menuItems,
        };

        // var expand = objectType.Fields.Values
        //     .Where(x => x.RBAC.CanRead(Context))
        //     .Select(x => x.Field)
        //     .OfType<ReferenceField>()
        //     .Where(x => x.ReferenceFieldOptions?.ContributeUserEvents.GetValueOrDefault() is ContributeUserEvents.Form or ContributeUserEvents.Always)
        //     .ToArray();
        //
        // if (expand.Length < 1) return menu;
        //
        // var flatObject = await _objectTypeService.GetFlatObjectAsync(Context, objectType, objectId);
        // foreach (var field in expand)
        // {
        //     if (!flatObject.TryGetValue(field.Name, out var foreignFieldValue)) continue;
        //
        //     _logger.LogInformation("Load user actions for {Field} {ObjectType}: {ForeignFieldName}={Value}", field.Name, field.ReferenceFieldOptions.ObjectType, field.ReferenceFieldOptions.ForeignFieldName ?? "_id", foreignFieldValue);
        //
        //     objectType = await _objectTypeService.GetAsync(Context, field.ReferenceFieldOptions.ObjectType);
        //     if (!objectType.RBAC.Can(Context, ObjectTypePermission.Read)) continue;
        //
        //     var id = foreignFieldValue switch
        //     {
        //         Guid uuid => uuid,
        //         string str => Guid.TryParse(str, out var uuid) ? uuid : default(Guid?),
        //         _ => default(Guid?)
        //     };
        //
        //     Dictionary<string, object> relatedObject;
        //     if ((field.ReferenceFieldOptions.ForeignFieldName ?? Model.IdFieldName) == Model.IdFieldName && id.HasValue)
        //     {
        //         relatedObject = await _objectTypeService.GetFlatObjectAsync(Context, objectType, id.Value);
        //     }
        //     else
        //     {
        //         relatedObject = await _objectTypeService.GetFlatReferencedObjectAsync(Context, objectType, field.ReferenceFieldOptions.ForeignFieldName ?? "_id", foreignFieldValue);
        //         if (relatedObject.TryGetGuidParam(Model.IdFieldName, out var relatedObjectId))
        //         {
        //             id = relatedObjectId;
        //         }
        //     }
        //
        //     if (!id.HasValue || relatedObject == null)
        //     {
        //         _logger.LogError("Related Object not found: {ObjectType} {ForeignField}={Value}", field.ReferenceFieldOptions.ObjectType, field.ReferenceFieldOptions.ForeignFieldName ?? "_id", foreignFieldValue);
        //         continue;
        //     }
        //
        //     (_, menuItems) = await _objectTypeService.GetUserActionsMenuItemsAsync(Context, objectType, id, flatObject: relatedObject);
        //
        //     var label = field.Label ?? field.Name;
        //     if (relatedObject.TryGetStrParam(objectType.LookupFields?.Name ?? nameof(Model.Name), out var name))
        //     {
        //         label = $"{label}: {name}";
        //     }
        //
        //     menuItems = menuItems
        //         .Prepend(new ActionMenuItem
        //         {
        //             Name = "View",
        //             Label = "View...",
        //             Action = $"dataForm://api/v1/CustomObject/{objectType.Name}({id})/View",
        //         })
        //         .ToArray();
        //
        //     menu.Items = menu.Items
        //         .Append(new Menu
        //         {
        //             Name = field.Name,
        //             Label = label,
        //             Items = menuItems,
        //             Icon = Icons.More,
        //         })
        //         .ToArray();
        // }

        return menu;
    }

    /// <summary>
    /// Get Form for a specific object so it can use its data to seed fields 
    /// </summary>
    [HttpGet("/api/v1/{objectTypeName}({objectId})/[controller]({eventId})/DataForm")]
    public Task<Form> GetActionForExistingObjectAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] Guid eventId)
        => _service.BuildActionFormForObjectAsync(Context, objectTypeName, objectId, eventId, BuildRunContext());
    
    /// <summary>
    /// Execute action triggered by User Action (for one object)
    /// </summary>
    [HttpPost("/api/v1/{objectType}({objectId})/[controller]({eventId})/DataForm")]
    public Task<DataFormActionResponse> RunActionForObjectAsync([FromRoute] string objectType, [FromRoute] Guid objectId, [FromRoute] Guid eventId, [FromBody] DataFormActionRequest request)
        =>  _service.ExecuteForObjectAsync(Context, objectType, objectId, eventId, request);

    /// <summary>
    /// Get Form in the middle of flow 
    /// </summary>
    [HttpGet("/api/v1/{objectTypeName}({objectId})/Flow({flowRunId})/[controller]({eventId})/DataForm")]
    public Task<Form> GetActionAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] Guid eventId, [FromRoute] Guid flowRunId)
        => BuildActionFormForFlowRunAsync(objectTypeName, objectId, eventId, flowRunId);

    /// <summary>
    /// Execute action triggered by User Action (for one object)
    /// </summary>
    [HttpPost("/api/v1/{objectTypeName}({objectId})/Flow({flowRunId})/[controller]({eventId})/DataForm")]
    public Task<DataFormActionResponse> RunActionInFlowAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] Guid eventId, [FromRoute] Guid flowRunId, [FromBody] DataFormActionRequest request)
        => RunUserActionAsync(objectTypeName, objectId, eventId, flowRunId, request);

    /// <summary>
    /// Execute user action from form?
    /// </summary>
    [HttpPost]
    public async Task<DataFormActionResponse> TriggerActionAsync([FromBody] UserActionRequest request)
    {
        var result = await _service.ExecuteAsync(Context, request.ObjectType, request.EventId, request);
        return result;
    }

    [Obsolete("use custom object like every other object type?")]
    /// <summary>
    /// get list of user actions? 
    /// </summary>
    [Authorize("managerplus")]
    [HttpPost("DataView")]
    [Produces("text/csv", "application/json")]
    public async Task<DataViewResponse> DataViewAsync(
        [FromBody] DataViewRequest request
    )
    {
        var response = await _service.GetDataViewAsync(Context, request);
        return response;
    }

    [Obsolete("probably can be replaced by always having the dataview/dataform/datapage/... add them explicitly")]
    /// <summary>
    /// Get user actions for a given object type 
    /// </summary>
    [HttpGet("/api/v1/{objectType}/[controller]")]
    [ProducesResponseType(typeof(UserAction[]), 200)]
    public async Task<IActionResult> GetActionsAsync([FromRoute] string objectType, bool multiple = false)
    {
        var list = await _service.GetAsync(Context, objectType, multiple);
        var result = list.Select(x => new UserAction
        {
            EventId = x.Id,
            Name = (x.Trigger as UserTrigger).Name,
            Form = (x.Trigger as UserTrigger).Form,
        });

        // TODO: should be filtered based on the flow for the selected objects???
        // ...

        return Ok(result);
    }

    [Obsolete("use custom object like every other object type?")]
    /// <summary>
    /// get Add/Edit from for a UserAction 
    /// </summary>
    [HttpGet("DataForm")]
    public async Task<Form> GetActionAsync([FromQuery] Guid? id)
    {
        var eventType = default(EventType);
        if (id.HasValue)
        {
            // edit
            // ...
            eventType = await _connection.Filter<EventType>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Id, id)
                .FirstOrDefaultAsync();

            if (eventType == null) throw new NotFoundException(nameof(EventType), id);
        }

        return new Form
        {
            Name = "User Action",
            Fields = new FormField[]
            {
                new HiddenField
                {
                    Name = "_id",
                    DefaultValue = eventType?.Id,
                },
                new TextField
                {
                    Name = nameof(EventType.Name),
                    DefaultValue = eventType?.Name,
                },
                new TextField
                {
                    Name = nameof(EventType.Description),
                    DefaultValue = eventType?.Description,
                },
                new ReferenceField
                {
                    Name = nameof(EventType.ObjectType),
                    Label = "Object Type",
                    DefaultValue = eventType?.ObjectType,
                    ReferenceFieldOptions =
                    {
                        ObjectType = nameof(ObjectType)
                    },
                    Enable = eventType == null
                        ? null
                        : new[]
                        {
                            "false"
                        },
                }
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = eventType != null ? FormAction.Update : FormAction.Add,
                }
            }
        };
    }

    [Obsolete("use custom object like every other object type?")]
    /// <summary>
    /// Process add/edit for a "UserAction" object 
    /// </summary>
    [HttpPost("DataForm")]
    public async Task<DataFormActionResponse> AddEditAsync([FromBody] DataFormActionRequest request)
    {
        if (string.Equals(request.Action, FormAction.Add))
        {
            var evt = await _service.CreateAsync(
                Context,
                request.Parameters[nameof(EventType.Name)].ToString(),
                request.Parameters[nameof(EventType.Name)].ToString(),
                request.Parameters[nameof(EventType.Description)].ToString(),
                request.Parameters[nameof(EventType.ObjectType)].ToString()
            );

            return new DataFormActionResponse
            {
                Action = request.Action,
                Ids = new[]
                {
                    evt.Id
                },
                Success = true,
                NextUrl = null, // ?????
                Message = "User Action created"
            };
        }

        // edit
        // ...
        throw new NotImplementedException();
    }
}

public class UserAction
{
    public Guid EventId { get; set; }
    public string Name { get; set; }
    public Form Form { get; set; }
}