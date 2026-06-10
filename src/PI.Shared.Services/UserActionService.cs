using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Requests;
using PI.Shared.Services.ActionRunners;
using Operator = PI.Shared.Models.Expressions.Operator;
using Snapshot = PI.Shared.Models.Snapshot;

namespace PI.Shared.Services;

public class UserActionWithRunnersService(
    ILogger<UserActionWithRunnersService> logger,
    MongoConnection connection,
    ObjectTypeService objectTypeService,
    ActionRunnerService actionRunnerService,
    IMessageBroker messageBroker)
    : UserActionService(logger, connection, objectTypeService, messageBroker)
{
    /// <summary>
    /// execute custom action for an object 
    /// </summary>
    protected override async Task<IResult> ExecuteForObjectAsync(
        IEntityContext context, EventType eventType, IDictionary<string, object> request, FlowRun flowRun, ExpandoObject expandoObject, User user, ObjectType objectType, string description,
        Channel<IResult> channel = null, CancellationToken ct = default
    )
    {
        var state = new ActionRunState
        {
            Context = context,
            User = user,
            ObjectType = objectType,
            ExpandoObject = expandoObject,
            Description = description,
            EventType = eventType,
            FlowRun = flowRun,
        };

        var prepare = await PrepareAsync(state);
        if (prepare.IsError) return AsyncResultStream.Error(prepare.Status);

        var newEvent = BuildEvent(state.Context, state.User, request, state.ObjectType, state.ExpandoObject, state.EventTypeId, state.Description, state.ObjectFlowRunId);
        if (newEvent == null)
        {
            return AsyncResultStream.Unknown("Nothing to do");
        }

        // elevate context to account 
        // and if the flow doesn't match, load "right" object type before processing
        // no need (and probably should not) to reload the flat object here... when processing the events it will always get a fresh copy  
        var accountContext = new AccountContext(state.Context.AccountId.Value).WithActorFrom(state.Context);
        var flowObjectType = state.Flow.ObjectType != state.ObjectType.FullName ? await _objectTypeService.GetAsync(accountContext, state.Flow.ObjectType) : state.ObjectType;
        await actionRunnerService.ProcessEventAsync(accountContext, flowObjectType, state.ObjectId, state.FlatObject, state.Flow, newEvent, channel, ct);

        await AfterActionRanAsync(state, accountContext, newEvent);

        // var nextUrl = await CalculateNextUrl(state, request.Parameters, newEvent);
        //
        // return new DataFormActionResponse(request)
        // {
        //     Success = runResult?.Success ?? true,
        //     Ids = [state.ObjectId],
        //     Message = runResult?.ErrorMessage ?? state.Trigger.Message,
        //     NextUrl = nextUrl,
        //     RunId = state.ObjectFlowRunId,
        // };

        return Result.Success(new { End = true });
    }


    /// <summary>
    /// Execute user action for a single object
    /// it will use action runner service to execute actions synchronously whenever possible 
    /// </summary>
    protected override async Task<DataFormActionResponse> ExecuteForObjectAsync(IEntityContext context, EventType eventType, DataFormActionRequest request, FlowRun flowRun, ExpandoObject expandoObject, User user, ObjectType objectType, string description)
    {
        var state = new ActionRunState
        {
            Context = context,
            User = user,
            ObjectType = objectType,
            ExpandoObject = expandoObject,
            Description = description,
            EventType = eventType,
            FlowRun = flowRun,
        };

        var prepare = await PrepareAsync(state);
        if (prepare.IsError)
        {
            return new DataFormActionResponse(request)
            {
                Success = false,
                Message = prepare.Status,
            };
        }

        return await ExecuteForObjectAsync(state, request);
    }

    private async Task<DataFormActionResponse> ExecuteForObjectAsync(ActionRunState state, DataFormActionRequest request)
    {
        var meta = new Dictionary<string, object>(request.Parameters ?? Enumerable.Empty<KeyValuePair<string, object>>())
        {
            { nameof(DataFormActionRequest.Action), request.Action }
        };

        var newEvent = BuildEvent(state.Context, state.User, meta, state.ObjectType, state.ExpandoObject, state.EventTypeId, state.Description, state.ObjectFlowRunId);

        var runResult = default(FlowRunResult);
        if (newEvent != null)
        {
            // elevate context to account 
            // and if the flow doesn't match, load "right" object type before processing
            // no need (and probably should not) to reload the flatobject here... when processing the events it will always get a fresh copy  
            var accountContext = new AccountContext(state.Context.AccountId.Value).WithActorFrom(state.Context);
            var flowObjectType = state.Flow.ObjectType != state.ObjectType.FullName ? await _objectTypeService.GetAsync(accountContext, state.Flow.ObjectType) : state.ObjectType;
            await actionRunnerService.ProcessEventAsync(accountContext, flowObjectType, state.ObjectId, state.FlatObject, state.Flow, newEvent);

            runResult = await AfterActionRanAsync(state, accountContext, newEvent);
        }

        var nextUrl = await CalculateNextUrl(state, request.Parameters, newEvent);

        return new DataFormActionResponse(request)
        {
            Success = runResult?.Success ?? true,
            Ids = [state.ObjectId],
            Message = runResult?.ErrorMessage ?? state.Trigger.Message,
            NextUrl = nextUrl,
            RunId = state.ObjectFlowRunId,
        };
    }

    private async Task<IResult> PrepareAsync(ActionRunState state)
    {
        if (!state.ExpandoObject.TryGetGuidParam("_id", out var _objectId)) throw new BadRequestException("Invalid or missing id");
        state.ObjectId = _objectId;
        state.ObjectStatusId = state.ExpandoObject.GetOptionalGuid(nameof(IFlowObject.ObjectStatusId));
        var flowId = state.ExpandoObject.GetOptionalGuid(nameof(IFlowObject.FlowId));
        if (!flowId.HasValue) return Result.Error("Unknown Flow");

        state.FlowId = flowId.Value;

        if (state.Trigger.ObjectStatusId.HasValue)
        {
            if (!state.ObjectStatusId.HasValue || state.ObjectStatusId != state.Trigger.ObjectStatusId)
            {
                _logger.LogInformation("Ignore {ObjectId} because doesn't match {ObjectStatusId} ", state.ObjectId, state.Trigger.ObjectStatusId);

                // TODO: should it fire an event?
                // ...

                return Result.Error("Invalid Status");
            }
        }

        // TODO: should try to load flow for object and return error if there is no step defined for the event/status
        // ...
        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, state.Context.AccountId.Value)
            .Eq(x => x.Id, state.FlowId)
            .ElemMatchBuilder(f => f.Steps, q => q.Eq(x => x.EventIdTrigger, state.EventTypeId).In(x => x.CurrentStatusId, [null, state.ObjectStatusId]))
            // .IncludeField(x => x.Name)
            .FirstOrDefaultAsync();

        if (flow == null)
        {
            _logger.LogError("No step configured for {FlowId} {ObjectStatusId} {EventId}", state.FlowId, state.ObjectStatusId, state.EventTypeId);
            return Result.Error("Action not available for Object in its current status.");
        }

        state.Flow = flow;
        state.FlatObject = await _objectTypeService.RecursivelyFlattenAsync(state.Context, state.ObjectType, state.ExpandoObject);

        return Result.Success(state);
    }

    private async Task<FlowRunResult> AfterActionRanAsync(ActionRunState state, IEntityContext accountContext, FlowEvent newEvent)
    {
        FlowRunResult runResult;
        state.FlowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, state.Context.AccountId.Value)
            .Eq(x => x.Id, state.ObjectFlowRunId)
            .FirstOrDefaultAsync();

        // update (local copy of) flow run with last version of object?
        state.FlatObject = await _objectTypeService.GetFlatObjectAsync(accountContext, state.ObjectType, state.ObjectId);
        state.FlowRun.Objects[FlowRun.GetObjectAlias(newEvent.ObjectType)] = new ObjectWithType
        {
            ObjectType = newEvent.ObjectType,
            Object = state.FlatObject
        };

        // TODO: move this to the Actionrunnerservice ? 
        runResult = await _connection.Filter<FlowRunResult>()
            .Eq(x => x.AccountId, state.Context.AccountId.Value)
            .Eq(x => x.Id, state.FlowRun.Id)
            .FirstOrDefaultAsync();

        if (runResult != null)
        {
            // TODO: should it save to the mongo?
            // ...

            if (!string.IsNullOrEmpty(runResult.ResultType))
            {
                state.FlowRun.Objects[FlowRun.GetObjectAlias(runResult.ResultType)] = new ObjectWithType
                {
                    ObjectType = runResult.ResultType,
                    Object = runResult.Result,
                };
            }
        }

        return runResult;
    }

    private async Task<string> CalculateNextUrl(ActionRunState state, Dictionary<string, object> requestParameters, FlowEvent newEvent)
    {
        var nextUrl = state.Trigger.NextUrl;
        if (string.IsNullOrEmpty(nextUrl) && newEvent != null)
        {
            var result = await _objectTypeService.GetDefaultNextUrlAsync(state.Context, state.ObjectType, state.FlatObject, newEvent, state.EventType);
            if (result.IsError)
            {
                _logger.LogError("Failed to process response: {Error}", result.Status);
            }
            else if (result.IsSuccess)
            {
                nextUrl = result.Value;
            }
        }
        else if (nextUrl?.IndexOf("{{", StringComparison.Ordinal) >= 0)
        {
            IDictionary<string, object> dict = state.FlowRun?.BuildHandlebarsContext(newEvent) ?? BuildHandlebarsContext(newEvent, state.ObjectType.Name, state.FlatObject);

            // TODO: remove this?
            // this may not be necessary/desired anymore, now it can just be part of the flow
            // ...
            // if (trigger.RelatedObjects != null)
            // {
            //     // also load related objects so it can be used to compose next url
            //     var result = await LoadRelatedObjectsIntoHandlebarsContextAsync(context, trigger, dict, eventType.ObjectType);
            //     if (!result.IsSuccess)
            //     {
            //         return new DataFormActionResponse(request, result.Status);
            //     }
            // }

            // TODO: remove 
            // probably not needed since now we have access to the flowrun
            // ...
            dict["Request|Parameters"] = requestParameters;
            dict["id"] = state.ObjectId;
            dict["flowRunId"] = state.ObjectFlowRunId;

            nextUrl = ObjectTypeService.ProcessNextUrl(state.Context, dict, nextUrl);
        }

        return nextUrl;
    }
}

public class UserActionService
{
    protected readonly ILogger<UserActionService> _logger;
    protected readonly MongoConnection _connection;
    protected readonly ObjectTypeService _objectTypeService;
    private readonly IMessageBroker _messageBroker;

    public UserActionService(
        ILogger<UserActionService> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        IMessageBroker messageBroker
    )
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _messageBroker = messageBroker;
    }

    public static void UpdateActionsMenu(DataViewResponse response, List<MenuItem> items, bool allowNone = false, string name = "Actions")
    {
        if (items.Count < 1) return;

        var actionsMenu = new Menu
        {
            Name = name,
            Items = items.ToArray(),
            Icon = nameof(Icons.Action),
            Visible = allowNone ? null : ["selectedCount!='0'"]
        };

        items.Clear();

        response.View.Menu ??= new Menu
        {
            Name = "Actions",
        };

        if (response.View.Menu.Items?.Length > 0) items.AddRange(response.View.Menu.Items);
        items.Add(actionsMenu);

        response.View.Menu.Items = items.ToArray();
    }

    /// <summary>
    /// Get Action Form for Object
    /// WILL LOAD the object for objectTypeName but run the flow for the "ObjectType" in the flow field
    /// </summary>
    public async Task<PI.Shared.Form.Models.Form> BuildActionFormForObjectAsync(IEntityContext context, string objectTypeName, Guid objectId, Guid eventId, Dictionary<string, object> runContext)
    {
        var objectType = await _objectTypeService.GetAsync(context, objectTypeName);
        if (objectType == null) throw new NotFoundException("ObjectType");
        var obj = await _objectTypeService.GetFlatObjectAsync(context, objectType, objectId);
        if (obj == null) throw new NotFoundException(objectTypeName, objectId);

        var objectStatusId = obj.GetOptionalGuid(nameof(FlowObjectModel.ObjectStatusId));
        var flowId = obj.GetOptionalGuid(nameof(FlowObjectModel.FlowId));

        var action = flowId.HasValue ? await _objectTypeService.GetUserActionUsingFlowAsync(context, flowId.Value, eventId, objectStatusId) : await _objectTypeService.GetUserActionUsingObjectTypeAsync(context, objectType, eventId, objectStatusId);
        if (action == null) throw new NotFoundException("Event");

        var objects = new Dictionary<string, object>
        {
            { objectTypeName, obj },
        };

        // set/reset context to meet expectations
        runContext["Context"] = context.GetPlaceholders();
        runContext["Object"] = obj;
        runContext["Objects"] = objects;
        runContext["Event"] = new Dictionary<string, object>
        {
            { Model.IdFieldName, eventId },
            { nameof(EventType.ObjectType), objectType.FullName },
        };

        await loadObjectAsync(nameof(User), context.UserId);
        await loadObjectAsync(nameof(Organization), context.OrganizationId);
        await loadObjectAsync(nameof(Account), context.AccountId);

        return await BuildUserActionFormAsync(context, action, runContext);

        async Task loadObjectAsync(string otName, Guid? id)
        {
            if (!id.HasValue) return;
            var ot = await _objectTypeService.GetAsync(context, otName);
            if (ot == null) throw NotFoundException.New(otName);
            var loadedObj = await _objectTypeService.GetFlatObjectAsync(context, ot, id.Value);
            if (loadedObj == null) throw NotFoundException.New(otName);
            objects.TryAdd(otName, loadedObj);
        }
    }

    /// <summary>
    /// Build Action Form for existing Object 
    /// </summary>
    public async Task<PI.Shared.Form.Models.Form> BuildUserActionFormAsync(IEntityContext context, EventType eventType, IDictionary<string, object> runContext, AppDataView appDataView = null)
    {
        var trigger = eventType.Trigger as UserTrigger;
        var form = trigger?.Form;
        if (form == null) throw new NotFoundException("Form");

        // TODO: load flow and check whether there is something setup to continue?
        // if (ExpressionEvaluatorService.TryResolve( Context, runContext, "{{Object.FlowId}}", out var flowIdObj))
        // {
        //     var flowId = flowIdObj switch
        //     {
        //         string str => Guid.TryParse(str, out var uuid) ? uuid : null,
        //         Guid uuid => uuid,
        //         _ => default(Guid?)
        //     };
        //
        //     if (flowId.HasValue)
        //     {
        //         var flow = await _connection.Filter<Flow>()
        //             .Eq(x => x.AccountId, Context.AccountId.Value)
        //             .Eq(x => x.Id, flowId.Value)
        //             .IncludeField(x => x.Steps)
        //             .FirstOrDefaultAsync();
        //
        //         if (flow != null)
        //         {
        //             
        //
        //         }
        //     }
        // }

        if (!runContext.TryGetValue("Request|Parameters", out var requestParametersObj) || requestParametersObj is not Dictionary<string, object> requestParameters)
        {
            requestParameters = new Dictionary<string, object>();
        }

        foreach (var field in form.Fields)
        {
            // TODO: why not allow to override default value with parameter?
            // ...
            if (field.DefaultValue != null) continue;

            // TODO: handle arrays (or other types)?
            // ...
            if (requestParameters.TryGetValue(field.ApiName ?? field.Name, out var paramValueObj) && paramValueObj is string paramValue)
            {
                // TODO: try to use autoconvert?
                // ...
                field.DefaultValue = field.AutoConvert(paramValue);
            }
        }

        if (appDataView != null)
        {
            runContext.TryAdd("Request|AppDataViewId", appDataView.Id);
        }

        if (trigger.RelatedObjects != null)
        {
            // it will look for Objects.{{eventType.ObjectType}} and fallback to {{Object}}
            var loadedObjectsResult = await LoadRelatedObjectsIntoHandlebarsContextAsync(context, trigger, runContext, eventType.ObjectType);
            if (!loadedObjectsResult.IsSuccess)
            {
                _logger.LogError("Failed to load related Objects: {Error}", loadedObjectsResult.Status);
                throw new Exception(loadedObjectsResult.Status);
            }
        }

        foreach (var field in form.Fields ?? Enumerable.Empty<FormField>())
        {
            field.FillPlaceHolders(context, runContext);

            // also allow fields to define a default value that is a template
            // does not check the AllowExpressions as that it is really about allowing the user to type 
            // a template as the value
            if (field?.DefaultValue is not string defaultValue) continue;
            if (!defaultValue.Contains("{{")) continue;

            if (ExpressionEvaluatorService.TryResolve(context, runContext, defaultValue, out var value))
            {
                field.DefaultValue = value;
            }
            else
            {
                _logger.LogError("Failed to calculate default value for {Field}: {DefaultValue}", field.Name, defaultValue);
                // ...
            }
        }

        if (form.Actions == null || form.Actions.IsEmpty()) return form;

        foreach (var action in form.Actions)
        {
            if (action.Action == null || !action.Action.Contains("{{")) continue;

            if (ExpressionEvaluatorService.TryResolve(context, runContext, action.Action, out var value))
            {
                action.Action = value?.ToString();
            }
            else
            {
                _logger.LogError("Couldn't resolve action {Expression}", action.Action);
            }
        }

        return form;
    }

    /// <summary>
    /// Execute user action for a known single object
    /// - it will not enforce that the flow/event type has to match the object type.
    /// </summary>
    public async Task<DataFormActionResponse> ExecuteForObjectAsync(IEntityContext context, string objectTypeName, Guid objectId, Guid eventId, DataFormActionRequest request, FlowRun flowRun = null)
    {
        // override selected ids just in case...
        request.SelectedIds =
        [
            objectId
        ];

        var objectType = await _objectTypeService.GetAsync(context, objectTypeName);
        if (objectType == null)
        {
            return new DataFormActionResponse(request, $"{objectTypeName} type not found");
        }

        if (!objectType.TryGetObjectTypeFromFlowField(out var flowObjectType))
        {
            return new DataFormActionResponse(request, $"{objectTypeName}: flow field not found");
        }

        if (flowObjectType != objectType.FullName)
        {
            // the flow is for a different object type
            // we can't simply use it because the current context may not have access to it
            // in reality the flow will elevate the access (to account) when running
            // so MAYBE we could elevate here and be done with it
            // FOR NOW, to avoid changing the current behavior.... 
            // we will let UserActionWithRunnersService handle it ... not changing the base class
            // ... 
        }

        var eventType = await _objectTypeService.GetUserActionAsync(context, flowObjectType, eventId);
        if (eventType?.Trigger is not UserTrigger userTrigger)
        {
            _logger.LogError("Invalid User Action: {EventId}", eventId);
            return new DataFormActionResponse(request, "Action not found");
        }

        request.Action ??= eventType.Name;

        var obj = await _objectTypeService.GetExpandoObjectByIdAsync(context, objectType, objectId);
        if (obj == null)
        {
            return new DataFormActionResponse(request, $"{objectType?.Label ?? objectType.Name} not found");
        }

        var description = eventType.Description ?? eventType.Name;

        // hack to change default event description
        if (request.Parameters != null && request.Parameters.TryGetValue(nameof(FlowEvent.Description), out var arg) && arg is string overrideDescription && !string.IsNullOrWhiteSpace(overrideDescription))
        {
            description = overrideDescription;
        }

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, context.UserId.Value)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return new DataFormActionResponse(request, "Invalid User");
        }

        return await ExecuteForObjectAsync(context, eventType, request, flowRun, obj, user, objectType, description);
    }

    public async Task<DataFormActionResponse> ExecuteAsync(IEntityContext context, string objectTypeName, Guid eventId, DataFormActionRequest request, FlowRun flowRun = null)
    {
        var objectType = await _objectTypeService.GetAsync(context, objectTypeName);
        var eventType = await _objectTypeService.GetUserActionUsingObjectTypeAsync(context, objectType, eventId, null);

        if (eventType?.Trigger is not UserTrigger trigger)
        {
            _logger.LogError("Invalid User Action: {EventId}", eventId);
            return new DataFormActionResponse(request)
            {
                Message = $"Invalid User Action for {objectTypeName}",
            };
        }

        return await ExecuteAsync(context, objectTypeName, eventType, request, flowRun);
    }

    public async Task<DataFormActionResponse> ExecuteAsync(IEntityContext context, string objectTypeName, EventType eventType, DataFormActionRequest request, FlowRun flowRun = null)
    {
        var objectType = await _objectTypeService.GetAsync(context, objectTypeName);
        if (objectType == null)
        {
            throw new NotFoundException("ObjectType");
        }

        return await ExecuteAsync(context, objectType, eventType, request, flowRun);
    }

    public async Task<IResult> ExecuteCustomActionAsync(IEntityContext context, ObjectType objectType, Guid objectId, EventType eventType, IDictionary<string, object> request, Channel<IResult> channel = null, CancellationToken ct = default, FlowRun flowRun = null)
    {
        // TODO: should filter  request parameters so only the only the ones in the form actually become part of the event meta
        // ...

        var description = eventType.Description ?? eventType.Name;

        // hack to change default event description
        if (request.TryGetValue(nameof(FlowEvent.Description), out var arg) && arg is string overrideDescription && !string.IsNullOrWhiteSpace(overrideDescription))
        {
            description = overrideDescription;
        }

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, context.UserId.Value)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return AsyncResultStream.Error("Invalid User");
        }

        var obj = await _objectTypeService.GetExpandoObjectByIdAsync(context, objectType, objectId);
        if (obj == null)
        {
            return AsyncResultStream.Error("Object Not Found");
        }

        return await ExecuteForObjectAsync(context, eventType, request, flowRun, obj, user, objectType, description, channel, ct);
    }

    public async Task<DataFormActionResponse> ExecuteAsync(IEntityContext context, ObjectType objectType, EventType eventType, DataFormActionRequest request, FlowRun flowRun = null)
    {
        // TODO: should filter  request parameters so only the only the ones in the form actually become part of the event meta
        // ...

        var description = eventType.Description ?? eventType.Name;

        // hack to change default event description
        if (request.Parameters.TryGetValue(nameof(FlowEvent.Description), out var arg) && arg is string overrideDescription && !string.IsNullOrWhiteSpace(overrideDescription))
        {
            description = overrideDescription;
        }

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, context.UserId.Value)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return new DataFormActionResponse(request)
            {
                Message = "Invalid User"
            };
        }

        if ((request.SelectedIds == null || request.SelectedIds.Length == 0) && !string.IsNullOrEmpty(request.View))
        {
            return await ExecuteAsync(context, eventType, request, user, objectType, description);
        }

        var objects = (await _objectTypeService.GetExpandoObjectsByIdAsync(context, objectType, request.SelectedIds))?.ToArray();
        if (objects == null)
        {
            return new DataFormActionResponse(request)
            {
                Message = "Missing objects"
            };
        }

        if (objects.Length == 1)
        {
            // single object
            return await ExecuteForObjectAsync(context, eventType, request, flowRun, objects[0], user, objectType, description);
        }

        return await ExecuteAsync(context, eventType, request, objects, user, objectType, description);
    }

    public async Task<Result<Guid?>> CreateSnapshotAsync(IEntityContext context, User user, UserTrigger trigger, AppDataView appDataView, Dictionary<string, object> parameters)
    {
        if (string.IsNullOrWhiteSpace(trigger.SnapshotObjectType)) return Result.Error<Guid?>("Missing Snapshot Object Type");

        var snapshotObjectType = await _objectTypeService.GetAsync(context, trigger.SnapshotObjectType);
        if (snapshotObjectType == null) return Result.Error<Guid?>("Invalid Snapshot Object Type");

        var snapshot = await _objectTypeService.AddObjectAsync(context, snapshotObjectType, parameters, obj =>
        {
            obj.TryAdd(nameof(Snapshot.CollectionName), "SnapshotData");
            obj[nameof(Snapshot.AppDataViewId)] = appDataView.Id;
            obj[nameof(Snapshot.CreatedById)] = user.Id;
            obj[nameof(Snapshot.SourceObjectType)] = appDataView.ObjectType;

            var entityIdCondition = appDataView.Criteria?.Conditions?.FirstOrDefault(x => x.FieldName == nameof(Snapshot.EntityId));
            var entityValue = entityIdCondition?.Operator switch
            {
                Operator.Eq => entityIdCondition.Value,
                Operator.In => entityIdCondition.Value switch
                {
                    IEnumerable<Guid> e => e.Count() == 1 ? e.FirstOrDefault() : null,
                    IEnumerable<string> e => e.Count() == 1 ? e.FirstOrDefault() : null,
                    IEnumerable<object> e => e.Count() == 1 ? e.FirstOrDefault() : null,
                    _ => null,
                },
                _ => null,
            };

            obj[nameof(Snapshot.EntityId)] = context.Role switch
            {
                EntityRoleId.Manager => context.OrganizationId,
                EntityRoleId.User => context.OrganizationId,
                EntityRoleId.Organization => context.OrganizationId,
                _ => entityValue.TryToParseObjectId(out var entityId) ? entityId : obj[nameof(Snapshot.EntityId)],
            };

            return null;
        });

        return snapshot;
    }

    private async Task<DataFormActionResponse> ExecuteAsync(IEntityContext context, EventType eventType, DataFormActionRequest request, User user, ObjectType objectType, string description)
    {
        if (eventType.Trigger is not UserTrigger trigger)
        {
            return new DataFormActionResponse(request, $"Invalid trigger");
        }

        // process view
        var appDataView = await _connection.GetProfileElementAsync<AppDataView>(context, q => q
            .Eq(x => x.ObjectType, objectType.Name)
            .Eq(x => x.Name, request.View)
            .Ne(x => x.IsActive, false)
        );

        if (appDataView == null)
        {
            return new DataFormActionResponse(request, $"{request.View} Not Found");
        }

        if (!string.IsNullOrWhiteSpace(trigger.SnapshotObjectType))
        {
            var snapshot = await CreateSnapshotAsync(context, user, trigger, appDataView, request.Parameters);
            if (!snapshot.IsSuccess) return new DataFormActionResponse(request, snapshot.Status);

            // hack to allow redirecting to snapshot created
            var nextUrl = trigger.NextUrl;
            if (snapshot.Value.HasValue && nextUrl?.IndexOf("{{id}}") > 0)
            {
                nextUrl = nextUrl.Replace("{{id}}", snapshot.Value.Value.ToString());
            }

            return new DataFormActionResponse(request, "Snapshot created", true)
            {
                Ids = new[]
                {
                    snapshot.Value.Value, // TODO: delete me? doesn't make sense to return an id that can't be loaded
                },
                NextUrl = nextUrl,
                Message = trigger.Message,
            };
        }

        var pipeline = PipelineDefinition<ExpandoObject, ExpandoObject>.Create(getStages());
        var cursor = _connection.Database
            .GetCollection<ExpandoObject>(objectType.CollectionName)
            .Aggregate(pipeline, new AggregateOptions
            {
                BatchSize = 100,
            });

        var eventTypeId = eventType.Id;
        var success = new List<Guid>();

        while (await cursor.MoveNextAsync())
        {
            foreach (var obj in cursor.Current)
            {
                if (!obj.TryGetGuidParam("_id", out var objectId)) throw new BadRequestException("Invalid or missing id");

                if (trigger.ObjectStatusId.HasValue)
                {
                    if (!obj.TryGetGuidParam(nameof(IFlowObject.ObjectStatusId), out var objectStatusId) || objectStatusId != trigger.ObjectStatusId)
                    {
                        _logger.LogInformation("Ignore {ObjectId} because doesn't match {ObjectStatusId} ", objectId, trigger.ObjectStatusId);
                        continue;
                    }
                }

                var objectFlowRunId = Guid.NewGuid();
                var newEvent = BuildEvent(context, user, request, objectType, obj, eventTypeId, description, objectFlowRunId);
                if (newEvent == null) continue;

                await _messageBroker.DispatchAsync(newEvent);
                success.Add(objectId);
            }
        }

        return new DataFormActionResponse(request)
        {
            Success = true,
            Ids = success.ToArray(),
            Message = trigger.Message,
            NextUrl = trigger.NextUrl,
            RunId = null // multiple runs....
        };

        IEnumerable<BsonDocument> getStages()
        {
            // match
            var match = AppDataViewPipelineBuilder
                .New(_connection, context, appDataView, objectType)
                .BuildMatch();

            foreach (var stage in match) yield return stage;

            // sort
            if (!string.IsNullOrWhiteSpace(appDataView.OrderBy))
            {
                var direction = appDataView.OrderBy.StartsWith("-") ? -1 : 1;
                var orderBy = appDataView.OrderBy.StartsWith("-") ? appDataView.OrderBy[1..] : appDataView.OrderBy;
                orderBy = orderBy.Replace("|", ".");

                yield return new BsonDocument
                {
                    { "$sort", new BsonDocument { { orderBy, direction } } }
                };
            }

            // projection
            var projection = Builders<FlowObjectModel>.Projection
                    .Include(x => x.AccountId)
                    .Include(x => x.FlowId)
                    .Include(x => x.ObjectStatusId)
                    .Include(x => x.Name)
                ;

            yield return new BsonDocument
            {
                { "$project", _connection.Database.ToBsonDocument(projection) }
            };

            // limit
            yield return new BsonDocument
            {
                { "$limit", 1000 }
            };
        }
    }

    private async Task<DataFormActionResponse> ExecuteAsync(IEntityContext context, EventType eventType, DataFormActionRequest request, ExpandoObject[] objects, User user, ObjectType objectType, string description)
    {
        var trigger = (UserTrigger)eventType.Trigger;
        var eventTypeId = eventType.Id;

        var success = new List<Guid>();
        foreach (var obj in objects)
        {
            if (!obj.TryGetGuidParam("_id", out var objectId)) throw new BadRequestException("Invalid or missing id");

            if (trigger.ObjectStatusId.HasValue)
            {
                if (!obj.TryGetGuidParam(nameof(IFlowObject.ObjectStatusId), out var objectStatusId) || objectStatusId != trigger.ObjectStatusId)
                {
                    _logger.LogInformation("Ignore {objectId} because doesn't match {objectStatusId} ", objectId, trigger.ObjectStatusId);
                    continue;
                }
            }

            var objectFlowRunId = Guid.NewGuid();
            var newEvent = BuildEvent(context, user, request, objectType, obj, eventTypeId, description, objectFlowRunId);
            if (newEvent == null) continue;

            await _messageBroker.DispatchAsync(newEvent);
            success.Add(objectId);
        }

        return new DataFormActionResponse(request)
        {
            Success = true,
            Ids = success.ToArray(),
            Message = trigger.Message,
            NextUrl = trigger.NextUrl,
            RunId = null, // multiple runs 
        };
    }

    /// <summary>
    /// Execute using custom request, response payload
    /// </summary>
    protected virtual async Task<IResult> ExecuteForObjectAsync(
        IEntityContext context, EventType eventType, IDictionary<string, object> request, FlowRun flowRun, ExpandoObject expandoObject, User user, ObjectType objectType, string description,
        Channel<IResult> channel = null, CancellationToken ct = default
    )
    {
        throw new NotImplementedException("Not supported without action runners");

        // just to make the compiler happy
        await Task.CompletedTask;
        // yield break;
    }

    protected virtual async Task<DataFormActionResponse> ExecuteForObjectAsync(IEntityContext context, EventType eventType, DataFormActionRequest request, FlowRun flowRun, ExpandoObject expandoObject, User user, ObjectType objectType, string description)
    {
        var trigger = (UserTrigger)eventType.Trigger;
        var eventTypeId = eventType.Id;

        if (!expandoObject.TryGetGuidParam("_id", out var objectId)) throw new BadRequestException("Invalid or missing id");
        var objectStatusId = expandoObject.GetOptionalGuid(nameof(IFlowObject.ObjectStatusId));
        var flowId = expandoObject.GetOptionalGuid(nameof(IFlowObject.FlowId));

        if (trigger.ObjectStatusId.HasValue)
        {
            if (!objectStatusId.HasValue || objectStatusId != trigger.ObjectStatusId)
            {
                _logger.LogInformation("Ignore {objectId} because doesn't match {objectStatusId} ", objectId, trigger.ObjectStatusId);

                // TODO: should it fire an event?
                // ...

                return new DataFormActionResponse(request)
                {
                    Success = false,
                    Message = "Invalid Status",
                };
            }
        }

        if (flowId.HasValue)
        {
            // TODO: should try to load flow for object and return error if there is no step defined for the event/status
            // ...
            var flow = await _connection.Filter<Flow>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, flowId.Value)
                .ElemMatchBuilder(f => f.Steps, q => q.Eq(x => x.EventIdTrigger, eventTypeId).In(x => x.CurrentStatusId, [null, objectStatusId]))
                .IncludeField(x => x.Name)
                .FirstOrDefaultAsync();

            if (flow == null)
            {
                _logger.LogError("No step configured for {FlowId} {ObjectStatusId} {EventId}", flowId, objectStatusId, eventTypeId);
                return new DataFormActionResponse(request)
                {
                    Success = false,
                    Message = "Action not available for Object in its current status.",
                };
            }
        }

        var objectFlowRunId = flowRun?.Id ?? Guid.NewGuid();
        var newEvent = BuildEvent(context, user, request, objectType, expandoObject, eventTypeId, description, objectFlowRunId);
        var flatObject = await _objectTypeService.RecursivelyFlattenAsync(context, objectType, expandoObject);

        if (newEvent != null)
        {
            await _messageBroker.DispatchAsync(newEvent);
        }

        var nextUrl = trigger.NextUrl;
        if (string.IsNullOrEmpty(nextUrl) && newEvent != null)
        {
            var result = await _objectTypeService.GetDefaultNextUrlAsync(context, objectType, flatObject, newEvent, eventType);
            if (result.IsError)
            {
                _logger.LogError("Failed to process response: {Error}", result.Status);
            }
            else if (result.IsSuccess)
            {
                nextUrl = result.Value;
            }
        }
        else if (nextUrl?.IndexOf("{{", StringComparison.Ordinal) >= 0)
        {
            var handlebarsContext = BuildHandlebarsContext(newEvent, objectType.Name, flatObject);
            var dict = (IDictionary<string, object>)handlebarsContext;

            if (trigger.RelatedObjects != null)
            {
                // also load related objects so it can be used to compose next url
                var result = await LoadRelatedObjectsIntoHandlebarsContextAsync(context, trigger, dict, eventType.ObjectType);
                if (!result.IsSuccess)
                {
                    return new DataFormActionResponse(request, result.Status);
                }
            }

            dict["Request|Parameters"] = request.Parameters;
            dict["id"] = objectId;
            dict["flowRunId"] = objectFlowRunId;

            nextUrl = ObjectTypeService.ProcessNextUrl(context, dict, nextUrl);
        }

        return new DataFormActionResponse(request)
        {
            Success = true,
            Ids = new[] { objectId },
            Message = trigger.Message,
            NextUrl = nextUrl,
            RunId = objectFlowRunId,
        };
    }

    protected ExpandoObject BuildHandlebarsContext(FlowEvent evt, string objectType, IDictionary<string, object> obj, FlowRun run = null)
    {
        if (run != null)
        {
            // TODO: create using run
            // ...
        }

        var context = new Dictionary<string, object>
        {
            { nameof(FlowRun.InitialEvent), evt },
            { nameof(FlowRun.InitialObject), obj },
            {
                nameof(FlowRun.Objects), new Dictionary<string, object>
                {
                    { FlowRun.GetObjectAlias(objectType), obj }
                }
            },
            { "Object", obj },
        };

        if (evt != null)
        {
            context.Add("Event", evt);
        }

        return JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(context));
    }

    public async Task<EventType> CreateAsync(IEntityContext context, string eventName, string name, string description, string objectType)
    {
        var trigger = new UserTrigger
        {
            Name = name,
            Form = new Form.Models.Form
            {
                Name = name,
                Fields = new FormField[]
                {
                    new TextField
                    {
                        Name = "Description",
                        TextFieldOptions =
                        {
                            Multline = true,
                        },
                    }
                },
                Actions = new[]
                {
                    new FormAction
                    {
                        Name = FormAction.Add,
                        Enable = new[]
                        {
                            "Description"
                        }
                    }
                }
            },
            Role = EntityRoleId.Admin,
            AllowMultiple = false,
        };

        var evt = new EventType
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            ObjectType = objectType,
            AccountId = context.AccountId.Value,
            EntityId = context.AccountId.Value,
            Trigger = trigger,
        };

        await _connection.InsertAsync(evt);

        // TODO: add to profile
        // ...

        return evt;
    }

    public async Task<List<EventType>> GetAsync(IEntityContext context, string objectType, bool multiple)
    {
        var query = _connection.Filter<EventType>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.ObjectType, objectType);

        if (multiple)
        {
            query.OfTypeBuilder<EventType, Trigger, UserTrigger>(
                x => x.Trigger,
                q => q.Eq(y => y.AllowMultiple, true)
            );
        }
        else
        {
            query.OfType<EventType, Trigger, UserTrigger>(x => x.Trigger);
        }

        return await query.FindAsync();
    }

    public async Task<DataViewResponse> GetDataViewAsync(IEntityContext context, DataViewRequest request)
    {
        var objectType = default(string);
        if (request.Criteria.TryGetEqCondition(nameof(EventType.ObjectType), out var filter) && !string.IsNullOrEmpty(filter.Value?.ToString()))
        {
            objectType = filter.Value.ToString();
        }

        var aggregation = new AggregateStoredProcedure
        {
            Collection = nameof(EventType),
            Pipeline = new[]
            {
                "{ \"$set\": { \"EntityRole\": \"$Trigger.EntityRole\" } }",
                "{ \"$unset\": \"Trigger\" }"
            }
        };

        var query = _connection.Filter<EventType>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .OfType<EventType, Trigger, UserTrigger>(x => x.Trigger);

        if (objectType != null) query.Eq(x => x.ObjectType, filter.Value.ToString());

        /*
        if (multiple)
        {
            query.OfType<EventType, Trigger, UserTrigger>(
                x => x.Trigger,
                q => q.Eq(x => x.ObjectType, objectType).Eq(x => x.AllowMultiple, true)
            );
        }
        else
        {
            query.OfType<EventType, Trigger, UserTrigger>(
                x => x.Trigger,
                q => q.Eq(x => x.ObjectType, objectType)
            );
        }
        */

        var response = new DataViewResponse
        {
            Request = request,
            Result = await query.DipperAsync<object>(aggregation),
            View = new DataView
            {
                Name = "UserActions",
                Title = "User Actions",
                KeyField = Model.IdFieldName,
                IsSelectable = true,
                Searchable = false,
                Fields = new FormField[]
                {
                    new TextField
                    {
                        Name = nameof(EventType.Name),
                        Label = "Name"
                    },
                    new TextField
                    {
                        Name = nameof(EventType.ObjectType),
                        Label = "Object Type"
                    },
                    new TextField
                    {
                        Name = nameof(UserTrigger.Role),
                        Label = "Role"
                    },
                    new HiddenField
                    {
                        Name = Model.IdFieldName,
                        Label = "Id"
                    },
                },
                Menu = new Menu
                {
                    Name = "EditMenu",
                    Items = new MenuItem[]
                    {
                        new ActionMenuItem
                        {
                            Name = FormAction.Add,
                            Action = "dataForm://api/v1/UserAction",
                            Icon = nameof(Icons.Add),
                            Visible = new[]
                            {
                                "selectedCount=='0'"
                            }
                        },
                        new ActionMenuItem
                        {
                            Name = FormAction.Edit,
                            Action = "dataForm://api/v1/UserAction",
                            Visible = new[]
                            {
                                "selectedCount=='1'"
                            }
                        }
                    }
                },
                FilterForm = new Form.Models.Form
                {
                    Name = "UserActions#Filter",
                    Fields = new FormField[]
                    {
                        new ReferenceField
                        {
                            Name = nameof(EventType.ObjectType),
                            Label = "Object Type",
                            DefaultValue = objectType,
                            ReferenceFieldOptions =
                            {
                                ObjectType = nameof(ObjectType)
                            }
                        }
                    }
                }
            }
        };

        return response.UpdateFields();
    }

    // private static async Task<FlowEvent> BuildOrgEventsAsync(IEntityContext context, DataFormActionRequest request, Organization org, Guid eventId, string description, User user, Guid flowRunId)
    // {
    //     var meta = new Dictionary<string, object>(request.Parameters);
    //     meta.TryAdd("Entity", org.Name);
    //     meta.TryAdd("Actor", user.Name);
    //
    //     return new EntityEvent(org)
    //     {
    //         Description = description,
    //         Actor = context.Actor(),
    //         RefValues = new KeyValuePair<string, object>[]
    //         {
    //             // new("UserId", user.Id),
    //             // new("OrganizationId", user.OrganizationId),
    //             // new("OrganizationId", org.Id),
    //             new("EntityId", context.UserId.Value),
    //             new("EntityId", org.Id),
    //         },
    //         MetaValues = meta,
    //         RunId = flowRunId,
    //     };
    // }

    /// <summary>
    /// Build event ... may return null if can't find the information on the object to build it 
    /// </summary>
    public static FlowEvent BuildEvent(IEntityContext context, User user, DataFormActionRequest request, ObjectType objectType, ExpandoObject expandoObject, Guid eventId, string description, Guid flowRunId)
    {
        var meta = new Dictionary<string, object>(request.Parameters ?? Enumerable.Empty<KeyValuePair<string, object>>())
        {
            { nameof(DataFormActionRequest.Action), request.Action }
        };

        return BuildEvent(context, user, meta, objectType, expandoObject, eventId, description, flowRunId);
    }

    protected static FlowEvent BuildEvent(IEntityContext context, User user, IDictionary<string, object> requestParameters, ObjectType objectType, ExpandoObject expandoObject, Guid eventId, string description, Guid flowRunId)
    {
        if (!expandoObject.TryGetGuidParam(nameof(IFlowObject.FlowId), out var flowId)) return null;
        if (!expandoObject.TryGetGuidParam("_id", out var objectId)) return null;
        if (!expandoObject.TryGetGuidParam(nameof(IFlowObject.AccountId), out var accountId)) return null;

        var safeObjectTypeName = objectType.SafeFullName;
        var objectTypeFullName = objectType.FullName;

        // override the object type with the "object type" from flow field  
        if (objectType.TryGetObjectTypeFromFlowField(out var ot))
        {
            objectTypeFullName = ot;
            safeObjectTypeName = ObjectType.GetSafeFullName(ot);
        }

        var meta = new Dictionary<string, object>(requestParameters ?? Enumerable.Empty<KeyValuePair<string, object>>());

        if (expandoObject.TryGetStrParam(nameof(IFlowObject.Name), out var name))
        {
            meta.TryAdd(safeObjectTypeName, name);
        }

        meta.TryAdd("Actor", user.Name);

        var refs = new List<KeyValuePair<string, object>>
        {
            new($"{safeObjectTypeName}Id", objectId),
            new("EntityId", user.Id),
        };

        if (user.OrganizationId.HasValue) refs.Add(new KeyValuePair<string, object>("EntityId", user.OrganizationId.Value));

        return new GenericFlowEvent
        {
            ObjectType = objectTypeFullName,
            TargetId = objectId,
            AccountId = accountId,
            StatusId = expandoObject.GetOptionalGuid(nameof(IFlowObject.ObjectStatusId)),
            FlowId = flowId,
            Description = description,
            Actor = context.Actor(),
            RefValues = refs,
            MetaValues = meta,
            RunId = flowRunId,
            EventTypeId = eventId,
        };
    }

    /// <summary>
    /// Load into "runContext" related objects for the trigger AND current user + org
    /// - it will load into runContext["Objects"] as if it was created from a flow run
    /// - it will look for Objects.{{eventType.ObjectType}} and fallback to {{Object}} 
    /// </summary>
    private async Task<Result<Dictionary<string, ObjectWithType>>> LoadRelatedObjectsIntoHandlebarsContextAsync(IEntityContext context, UserTrigger trigger, IDictionary<string, object> handlebarsContext, string objectTypeForEvent)
    {
        if (!handlebarsContext.TryGetValue(nameof(FlowRun.Objects), out var objects) || objects is not IDictionary<string, object> objectsDict)
        {
            objectsDict = new Dictionary<string, object>();
            handlebarsContext[nameof(FlowRun.Objects)] = objectsDict;
        }

        // since we may be running a flow for the base object type of another object type 
        //  and then the Object will be in {{Objects."derivedObjectType"}} instead of {{Objects."objectType for event == flow.objectType"}}
        //  1) look for object in the list of objects (e.g. {{Objects.[objectTypeForEvent]}})
        if (!objectsDict.TryGetValue(FlowRun.GetObjectAlias(objectTypeForEvent), out var currentObject) || currentObject is not IDictionary<string, object> currentObjectDict)
        {
            // 2) fallback to Object in context (e.g. {{Object}}) 
            if (!handlebarsContext.TryGetValue("Object", out var triggerObject) || triggerObject is not IDictionary<string, object> triggerObjectDict)
            {
                return Result.Error<Dictionary<string, ObjectWithType>>("Couldn't find current object");
            }

            currentObjectDict = triggerObjectDict;
        }

        var result = await _objectTypeService.LoadRelatedObjectsAsync(context, trigger, objectTypeForEvent, currentObjectDict);
        if (!result.IsSuccess) return result;

        // load into run context (as if it was resolved by the buildHandlebars using a flowRun)
        foreach (var kvp in result.Value)
        {
            objectsDict.TryAdd(kvp.Key, kvp.Value.Object);
        }

        return result;
    }
}