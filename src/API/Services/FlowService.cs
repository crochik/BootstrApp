using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using FlowActions;
using Messages.Flow;
using MongoDB.Bson;
using Newtonsoft.Json;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;
using Entity = PI.Shared.Models.Entity;
using EventType = Controllers.Models.EventType;
using User = PI.Shared.Models.User;

namespace Services;

// TODO: move to a different "flow app"
public class FlowService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly IEventTypeAdapter _eventTypeAdapter;
    private readonly Dictionary<Guid, IFlowActionBuilder> _builders;

    public FlowService(
        ILogger<FlowService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        IEventTypeAdapter eventTypeAdapter,
        IEnumerable<IFlowActionBuilder> builders
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
        _eventTypeAdapter = eventTypeAdapter;

        _builders = builders.ToDictionary(b => b.Id);
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, EventIds.AllRoute);
        MessageBroker.Bind(messageQueue, EventIds.ErrorRoute);
        mapper.RegisterAll<FlowEvent>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var parts = evt.RoutingKey.Split('.');
            var eventId = Guid.Parse(parts[1]);

            switch (evt.Body)
            {
                case FlowEvent flowEvent:
                    await ProcessEventAsync(eventId, flowEvent, parts[2] == "error");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }

    private async Task<IEnumerable<FlowStep>> GetStepsAsync(Guid flowId, Guid eventId, Guid? objectStatusId)
    {
        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.Id, flowId)
            .FirstOrDefaultAsync();

        var steps = flow?.Steps ?? Enumerable.Empty<FlowStep>();

        // TODO: could filter in the query but...
        return steps.Where(x =>
            (!x.CurrentStatusId.HasValue || x.CurrentStatusId.Value == objectStatusId) && x.EventIdTrigger == eventId
        );
    }

    private async Task ProcessEventAsync<T>(Guid eventId, T evt, bool failed) where T : FlowEvent
    {
        using var scope = Logger.AddScope(new
        {
            evt.AccountId,
            evt.FlowId,
            evt.TargetId,
            evt.ObjectType,
            EventTypeId = eventId,
            evt.StatusId,
            evt.RunId,
            Error = failed
        });

        var accountContext = new AccountContext(evt.AccountId).With(evt.Actor);

        var objectType = await _objectTypeService.GetAsync(accountContext, evt.ObjectType);
        if (objectType == null)
        {
            Logger.LogError("{ObjectType} not found", evt.ObjectType);
            return;
        }

        var rawObject = await _objectTypeService.GetExpandoObjectByIdAsync(accountContext, objectType, evt.TargetId);
        if (rawObject == null)
        {
            Logger.LogError("Object not found");
            return;
        }

        var dict = (IDictionary<string, object>)rawObject;

        var flowIds = objectType.GetFlowIds(rawObject).ToArray();
        if (flowIds.Length < 1)
        {
            Logger.LogError("Object is not part of any flow");
            return;
        }

        var flatObject = await _objectTypeService.RecursivelyFlattenAsync(accountContext, objectType, rawObject);

        if (failed)
        {
            // Logger.LogInformation("Add Failed event to flows");
            // foreach (var flowId in flowIds)
            // {
            //     var flowEvent = evt;
            //     if (flowId != evt.FlowId)
            //     {
            //         Logger.LogInformation("Cloning event for different {OtherFlowId}", flowId);
            //
            //         flowEvent = JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(evt));
            //         flowEvent.FlowId = flowId;
            //         flowEvent.RunId = Guid.NewGuid();
            //     }
            //
            //     // TODO: process using "standard" error action? 
            //     // ...
            //     var steps = Array.Empty<FlowStep>();
            //
            //     var runStep = new RunStep
            //     {
            //         Event = flowEvent,
            //         Steps = steps,
            //     };
            //
            //     await _objectTypeService.UpsertFlowRunAsync(accountContext, objectType, flatObject, runStep);
            // }

            var result = await _connection.Filter<FlowRun>()
                .Eq(x => x.Id, evt.RunId)
                .Update
                .Push(x => x.FinalEvents, evt)
                .UpdateOneAsync();
            
            if (result.ModifiedCount > 0)
            {
                Logger.LogInformation("Added Final (Error) Event to Flow");
            }
            else
            {
                Logger.LogInformation("Final (Error) Event is first in the Run");
            }
            return;
        }

        // success 
        // TODO: this could be a different field based on the flow field options
        // ... 
        var objectStatusId = default(Guid?);
        if (dict.TryGetGuidParam(nameof(IFlowObject.ObjectStatusId), out var value))
        {
            objectStatusId = value;
        }

        var flows = (
                await _connection.Filter<Flow>()
                    .Eq(x => x.AccountId, accountContext.AccountId)
                    .In(x => x.Id, flowIds)
                    .ElemMatchBuilder(
                        x => x.Steps,
                        q => q.Eq(x => x.EventIdTrigger, eventId)
                            .In(x => x.CurrentStatusId, [null, objectStatusId])
                    )
                    .FindAsync()
            )
            .ToDictionary(x => x.Id);

        if (flows.Count < 1)
        {
            Logger.LogInformation("No actions for any of the flows found");

            var result = await _connection.Filter<FlowRun>()
                .Eq(x => x.Id, evt.RunId)
                .Update
                .Push(x => x.FinalEvents, evt)
                .UpdateOneAsync();
            
            if (result.ModifiedCount > 0)
            {
                Logger.LogInformation("Added Final Event to Flow");
            }
            else
            {
                Logger.LogInformation("Final Event is first in the Run");
            }
            return;
        }

        var loadedObjects = default(Dictionary<string, ObjectWithType>);
        var eventType = await _eventTypeAdapter.GetByIdAsync(eventId);
        if (eventType?.Trigger is UserTrigger userTrigger && userTrigger.RelatedObjects != null && evt.Actor is AbstractAPIActor apiActor)
        {
            var user = apiActor.UserId.HasValue
                ? await _connection.Filter<Entity, User>()
                    .Eq(x => x.AccountId, accountContext.AccountId)
                    .Eq(x => x.Id, apiActor.UserId.Value)
                    .Ne(x => x.IsActive, false)
                    .FirstOrDefaultAsync()
                : null;

            var context = user?.Context ?? accountContext;
            var loaded = await _objectTypeService.LoadRelatedObjectsAsync(context, userTrigger, evt.ObjectType, flatObject);
            if (!loaded.IsSuccess)
            {
                Logger.LogError("Couldn't load related objects: {Error}", loaded.Status);
                return;
            }

            loadedObjects = loaded.Value;
        }

        foreach (var flowId in flowIds)
        {
            if (!flows.TryGetValue(flowId, out var flow)) continue;
            
            // var steps = (await GetStepsAsync(flowId, eventId, objectStatusId)).ToArray();
            // if (steps.Length < 1) continue;

            var steps = (flow?.Steps ?? Enumerable.Empty<FlowStep>())
                .Where(x => (!x.CurrentStatusId.HasValue || x.CurrentStatusId.Value == objectStatusId) && x.EventIdTrigger == eventId)
                .ToArray();
            
            var flowEvent = evt;
            if (flowId != evt.FlowId)
            {
                Logger.LogInformation("Cloning event for different {OtherFlowId}", flowId);

                flowEvent = JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(evt));
                flowEvent.FlowId = flowId;
                flowEvent.RunId = Guid.NewGuid();
            }

            await processFlowAsync(flowEvent, steps);
        }

        async Task processFlowAsync(T flowEvent, FlowStep[] steps)
        {
            using var scope1 = Logger.AddScope(new
            {
                EventType = eventType?.Name,
                flowEvent.RunId,
            });

            Logger.LogInformation("Found {Steps} steps", steps.Length);

            await _objectTypeService.UpsertFlowRunAsync(flatObject, flowEvent, steps, loadedObjects);

            foreach (var step in steps)
            {
                var builder = await GetActionBuilderAsync(accountContext, step.ActionId);
                if (builder == null)
                {
                    Logger.LogWarning("Unknown {ActionId}", step.ActionId);
                    continue;
                }

                try
                {
                    var (message, route) = builder.Build(accountContext, flowEvent, step.Options);
                    if (message == null)
                    {
                        Logger.LogWarning("{Action} => failed", builder.GetType().Name);
                        continue;
                    }

                    Logger.LogInformation(
                        "{Action} => {Route}",
                        builder.GetType().Name,
                        route
                    );

                    await MessageBroker.PublishAsync(route, message);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to trigger action");
                }
            }
        }
    }

    // public async Task<ParsedOptions> ParseAsync(ParseContext context)
    // {
    //     var builder = GetActionBuilder(context.ActionId);
    //     if (builder == null)
    //     {
    //         return ParsedOptions.Failed($"Unknown ActionId: {context.ActionId}");
    //     }
    //
    //     return await builder.ParseAsync(context);
    // }

    [Obsolete("migrate to new flowbuilder")]
    public IEnumerable<IFlowActionBuilder> GetForFlowType(IEntityContext context, Guid? flowTypeId)
    {
        // TODO: filter by flowtype id (and context?)
        // ... 
        return _builders.Values;
    }

    [Obsolete("migrate to new flowbuilder")]
    public IEnumerable<IFlowActionBuilder> GetActions(IEntityContext context, Flow flow, IEventType eventType)
    {
        var objectType = flow?.ObjectType;
        var rows = _builders
            .Where(kv => kv.Value.IsValidTrigger(objectType) || kv.Value.IsValidTrigger(eventType.ObjectType))
            .Select(kv => kv.Value);
        return rows;
    }

    /// <summary>
    /// Get Builder for an action
    /// supports Generic Actions
    /// </summary>
    private async Task<IFlowActionBuilder> GetActionBuilderAsync(IEntityContext context, Guid actionId)
    {
        // step.ActionId
        if (_builders.TryGetValue(actionId, out var builder))
        {
            return builder;
        }

        var genericActions = await _connection.GetProfileElementsAsync<GenericAction, Guid>(
            context,
            action => action.ActionId,
            q => q.Eq(x => x.ActionId, actionId));

        var action = genericActions.FirstOrDefault();
        return action != null ? new GenericActionBuilder(action, _objectTypeService) : null;
    }

    private async Task<IEnumerable<IFlowActionBuilder>> GetActionBuilderAsync(IEntityContext context, Guid flowId,
        Guid eventTypeId)
    {
        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .FirstOrDefaultAsync();

        if (flow == null) throw new NotFoundException(nameof(Flow), flowId);

        var objectType = await _objectTypeService.GetAsync(context, flow?.ObjectType);
        var allTypes = objectType.GetLoadedBaseObjectTypeNames()
            .Select(x => $"{x}*")
            .Append(objectType.Name)
            .ToArray();

        // TODO: bake in into the query the "valid trigger" criteria
        // ...
        var genericActions =
            await _connection.GetProfileElementsAsync<GenericAction, Guid>(context, action => action.ActionId);

        var actionBuilders = genericActions.Select(x => new GenericActionBuilder(x, _objectTypeService))
            .Concat(_builders.Values);

        var eventType = await _eventTypeAdapter.GetByIdAsync(eventTypeId);

        return actionBuilders.Where(a =>
            allTypes.Any(x => a.IsValidTrigger(x) || a.IsValidTrigger(eventType?.ObjectType)));
    }

    /// <summary>
    /// Get possible actions
    /// supports Generic Actions
    /// TODO: include objectStatus and filter actions based on the EntityContext profile
    /// ...  
    /// </summary>
    public async Task<IEnumerable<KeyValuePair<Guid, string>>> GetActionBuilderNamesAsync(IContextWithActor context,
        Guid flowId, Guid eventTypeId)
    {
        var validBuilders = await GetActionBuilderAsync(context, flowId, eventTypeId);
        return validBuilders.Select(x => new KeyValuePair<Guid, string>(x.Id, x.Description ?? x.Name));
    }

    /// <summary>
    /// Create form for a new step triggered by the event
    /// </summary>
    public async Task<Form> GetAddStepFormAsync(IEntityContext context, Guid flowId, Guid eventTypeId, Guid actionId,
        Guid? objectStatusId)
    {
        var builder = await GetActionBuilderAsync(context, actionId);
        if (builder == null) throw new Exception("Invalid ActionId");

        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .FirstOrDefaultAsync();

        if (flow == null) throw NotFoundException.New<Flow>(flowId);

        return await builder.GetFormAsync(new FlowActionContext(context, flow)
        {
            EventTypeId = eventTypeId,
        }, objectStatusId);
    }

    public async Task<Result<FlowStepsClipboard>> CopyToClipboardAsync(IEntityContext context, Guid flowId, Guid stepId,
        bool cut)
    {
        var (flow, step) = await GetStepOrThrowAsync(context, flowId, stepId);
        return await CopyToClipboardAsync(context, flow, step, cut ? ClipboardOperation.Cut : ClipboardOperation.Copy);
    }

    private async Task<Result<FlowStepsClipboard>> CopyToClipboardAsync(IEntityContext context, Flow flow,
        FlowStep step, ClipboardOperation operation)
    {
        var list = new List<FlowStep> { step };
        var hash = new HashSet<Guid> { step.Id };

        for (var c = 0; c < list.Count; c++)
        {
            var s = list[c];
            if (s.Options?.Output == null) continue;

            var eventIds = s.Options.Output
                .Where(x => x.EventId.HasValue)
                .Select(x => x.EventId)
                .ToHashSet();
            var steps = flow.Steps
                .Where(x => x.CurrentStatusId == step.CurrentStatusId && eventIds.Contains(x.EventIdTrigger))
                .Where(x => !hash.Contains(x.Id));

            foreach (var d in steps)
            {
                list.Add(d);
                hash.Add(d.Id);
            }
        }

        var clipboard = new FlowStepsClipboard
        {
            Id = Guid.NewGuid(),
            AccountId = context.AccountId.Value,
            EntityId = context.UserId.Value,
            CreatedOn = DateTime.UtcNow,
            ExpiresOn = DateTime.UtcNow.AddMinutes(10), // ???
            ObjectType = flow.ObjectType,
            FlowId = flow.Id,
            StepId = step.Id,
            Steps = list.ToArray(),
            Operation = operation,
            LastActor = context.Actor(),
            Name = list.Count > 1 ? $"\"{list[0].Description}\" + {list.Count - 1} Steps" : list[0].Description,
            Description = list.Count > 1
                ? $"\"{list[0].Description}\" + {list.Count - 1} Steps for {flow.ObjectType}'s Flow @ {DateTime.UtcNow.ToShortTimeString()}"
                : $"\"{list[0].Description}\" for {flow.ObjectType}'s Flow @ {DateTime.UtcNow.ToShortTimeString()}",
        };

        clipboard = await _connection.InsertAsync(clipboard);

        return Result.Success(clipboard);
    }

    public async Task<Form> GetPasteStepFormAsync(IContextWithActor context, Guid flowId, Guid? objectStatusId,
        Guid eventTypeId)
    {
        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .FirstOrDefaultAsync();

        if (flow == null) throw NotFoundException.New<Flow>(flowId);

        var eventType = await _connection.Filter<EventType>()
            .In(x => x.AccountId, [context.AccountId.Value, AccountIds.CSS])
            .Eq(x => x.Id, eventTypeId)
            // .Eq(x => x.ObjectType, flow.ObjectType)
            .FirstOrDefaultAsync();

        if (eventType == null) throw NotFoundException.New<EventType>(eventTypeId);
        if (eventType.Trigger is not UserTrigger && eventType.Trigger is not SystemTrigger &&
            eventType.Trigger is not ScheduledTrigger) throw new BadRequestException("Invalid Event Type");

        var objectStatus = default(ObjectStatus);
        if (objectStatusId.HasValue)
        {
            objectStatus = await _connection.Filter<ObjectStatus>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.ObjectType, flow.ObjectType)
                .Eq(x => x.Id, objectStatusId.Value)
                .FirstOrDefaultAsync();

            if (objectStatus == null) throw NotFoundException.New<ObjectStatus>(objectStatusId.Value);
        }

        return await GetPasteStepFormAsync(context, flow, null, eventType, objectStatus);
    }

    public async Task<Form> GetPasteStepFormAsync(IEntityContext context, Guid flowId, Guid stepId)
    {
        var (flow, step) = await GetStepOrThrowAsync(context, flowId, stepId);
        if (step.Options?.Output == null || step.Options.Output.Length < 1)
            return Form.BuildErrorForm("Step has no Output.", "Can't Paste");
        return await GetPasteStepFormAsync(context, flow, step);
    }

    private async Task<Form> GetPasteStepFormAsync(IEntityContext context, Flow flow, FlowStep step = null,
        EventType eventType = null, ObjectStatus objectStatus = null)
    {
        var clipboard = await _connection.Filter<Clipboard, FlowStepsClipboard>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .OrBuilder(
                q => q
                    .Eq(x => x.EntityId, context.EntityId.Value)
                    .Gt(x => x.ExpiresOn, DateTime.UtcNow),
                q => q
                    .Eq(x => x.IsShared, true)
            )
            .Eq(x => x.ObjectType, flow.ObjectType)
            .Ne(x => x.IsActive, false)
            .SortDesc(x => x.CreatedOn)
            .Limit(50)
            .IncludeField("_t")
            .IncludeFields(
                x => x.Id,
                x => x.Name,
                x => x.Description
            )
            .FindAsync();

        if (clipboard.Count < 1) return Form.BuildErrorForm("Clipboard is empty", "Can't Paste");

        return new Form
        {
            Name = "Paste",
            Title = clipboard.Count > 1 ? "Paste" : $"Paste \"{clipboard[0].Name}\"",
            Fields = getFields().ToArray(),
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Add",
                    Visible = new[] { "!Cut" },
                    Enable = new[] { Form.RequiredFieldsName },
                },
                new FormAction
                {
                    Name = "Move",
                    Visible = new[] { "Cut" },
                    Enable = new[] { Form.RequiredFieldsName },
                }
            }
        };

        IEnumerable<FormField> getFields()
        {
            if (clipboard.Count == 1)
            {
                yield return new HiddenField
                {
                    Name = nameof(Clipboard),
                    DefaultValue = clipboard[0].Id,
                };
            }
            else
            {
                yield return new SelectField
                {
                    Name = nameof(Clipboard),
                    DefaultValue = clipboard[0].Id,
                    SelectFieldOptions = new SelectFieldOptions
                    {
                        Items = clipboard.ToDictionary(x => x.Id, x => x.Description ?? x.Name),
                    },
                };
            }

            yield return new TextField
            {
                Name = nameof(Flow),
                DefaultValue = flow.Description ?? flow.Name,
                Enable = new[] { "false" },
            };

            if (step != null)
            {
                yield return new TextField
                {
                    Name = nameof(FlowStep),
                    Label = "Step",
                    DefaultValue = step.Description,
                    Enable = new[] { "false" },
                };

                yield return new SelectField
                {
                    Name = nameof(ActionOutput),
                    Label = "Output",
                    SelectFieldOptions = new SelectFieldOptions
                    {
                        Items = step.Options.Output?.ToDictionary(x => x.EventId, x => x.Description ?? x.Name),
                    },
                    IsRequired = true,
                };
            }

            if (objectStatus != null)
            {
                yield return new TextField
                {
                    Name = nameof(ObjectStatus),
                    Label = "Object Status",
                    DefaultValue = objectStatus.Description ?? objectStatus.Name,
                    Enable = new[] { "false" },
                };
            }

            if (eventType != null)
            {
                yield return new TextField
                {
                    Name = nameof(EventType),
                    Label = "Trigger",
                    DefaultValue = eventType.Description ?? eventType.Name,
                    Enable = new[] { "false" },
                };

                yield return new HiddenField
                {
                    Name = nameof(ActionOutput),
                    DefaultValue = eventType.Id,
                };
            }

            yield return new CheckboxField
            {
                Name = "RemoveFromClipboard",
                Label = "Remove from Clipboard",
                DefaultValue = true,
            };

            // yield return new CheckboxField
            // {
            //     Name = "Cut",
            //     Label = "Move (Cut)",
            //     DefaultValue = clipboard.Operation == ClipboardOperation.Cut,
            //     Visible = clipboard.Operation != ClipboardOperation.Cut ? new[] { "false" } : null,
            // };
        }
    }

    public async Task<Result<Flow>> PasteStepAsync(IEntityContext context, Guid flowId, Guid eventTypeId,
        Guid? objectStatusId, FlowStepsClipboard clipboard, Dictionary<string, object> parameters)
    {
        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .FirstOrDefaultAsync();

        if (flow == null) throw NotFoundException.New<Flow>(flowId);

        var eventType = await _connection.Filter<EventType>()
            .In(x => x.AccountId, [context.AccountId.Value, AccountIds.CSS])
            .Eq(x => x.Id, eventTypeId)
            // .Eq(x => x.ObjectType, flow.ObjectType)
            .FirstOrDefaultAsync();

        if (eventType == null) throw NotFoundException.New<EventType>(eventTypeId);
        if (eventType.Trigger is not UserTrigger && eventType.Trigger is not SystemTrigger &&
            eventType.Trigger is not ScheduledTrigger) throw new BadRequestException("Invalid Event Type");

        var objectStatus = default(ObjectStatus);
        if (objectStatusId.HasValue)
        {
            objectStatus = await _connection.Filter<ObjectStatus>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.ObjectType, flow.ObjectType)
                .Eq(x => x.Id, objectStatusId.Value)
                .FirstOrDefaultAsync();

            if (objectStatus == null) throw NotFoundException.New<ObjectStatus>(objectStatusId.Value);
        }

        return await PasteStepAsync(context, flow, clipboard, parameters, eventType: eventType,
            objectStatus: objectStatus);
    }

    public async Task<Result<Flow>> PasteStepAsync(IEntityContext context, Guid flowId, Guid stepId,
        FlowStepsClipboard clipboard, Dictionary<string, object> parameters)
    {
        var (flow, step) = await GetStepOrThrowAsync(context, flowId, stepId);
        if (step.Options?.Output == null || step.Options.Output.Length < 1)
            return Result.Error<Flow>("Step has not outputs");

        return await PasteStepAsync(context, flow, clipboard, parameters, step: step);
    }

    private async Task<Result<Flow>> PasteStepAsync(IEntityContext context, Flow flow, FlowStepsClipboard clipboard,
        Dictionary<string, object> parameters, FlowStep step = null, EventType eventType = null,
        ObjectStatus objectStatus = null)
    {
        if (clipboard == null) return Result.Error<Flow>("Item(s) no longer in the clipboard");

        // if (clipboard.Operation != ClipboardOperation.Cut || !parameters.TryGetParam("Cut", out var cutObj) || cutObj is not bool cut)
        // {
        //     cut = false;
        // }

        var cut = !clipboard.IsShared && clipboard.Operation == ClipboardOperation.Cut;
        if (clipboard.IsShared || !parameters.TryGetParam("RemoveFromClipboard", out var removeFromClipboardObj) ||
            removeFromClipboardObj is not bool removeFromClipboard)
        {
            removeFromClipboard = false;
        }

        if (!parameters.TryGetGuidParam(nameof(ActionOutput), out var eventTypeId))
        {
            if (eventType == null) return Result.Error<Flow>("Missing output");
            eventTypeId = eventType.Id;
        }

        var objectStatusId = step != null ? step.CurrentStatusId : objectStatus?.Id;

        if (cut) return await cutAsync();

        return await copyAsync();

        async Task<Result<Flow>> cutAsync()
        {
            if (clipboard.StepId == step?.Id) return Result.Error<Flow>("Can't move step into itself");
            if (clipboard.Steps[0].CurrentStatusId != objectStatusId)
                return Result.Error<Flow>("Can't move to a different Status yet");
            var srcStep = flow.Steps.FirstOrDefault(x => x.Id == clipboard.StepId);
            if (srcStep == null) return Result.Error<Flow>("Can't move Step. It is no longer part of the flow");
            if (srcStep.EventIdTrigger == eventTypeId) return Result.Unknown<Flow>("Nothing to do");

            flow = await _connection.Filter<Flow>()
                .Eq(x => x.Id, flow.Id)
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Update
                .ArrayFilter(new BsonDocument($"step.{Model.IdFieldName}", srcStep.Id.ToString()))
                .Set($"{nameof(Flow.Steps)}.$[step].{nameof(FlowStep.EventIdTrigger)}", eventTypeId.ToString())
                .UpdateAndGetOneAsync();

            // TODO: FIRE EVENT
            // ...

            // update clipboard
            var query = _connection.Filter<Clipboard, FlowStepsClipboard>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, clipboard.Id)
                .Update
                .Set(x => x.Operation, ClipboardOperation.Copy)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor());
            if (removeFromClipboard) query.Set(x => x.IsActive, false);

            await query.UpdateOneAsync();

            // TODO: FIRE EVENT
            // ...

            return Result.Success(flow);
        }

        async Task<Result<Flow>> copyAsync()
        {
            // copy
            var swap = new Dictionary<Guid, Guid?>
            {
                { clipboard.Steps[0].EventIdTrigger, eventTypeId },
                { clipboard.Steps[0].CurrentStatusId ?? Guid.Empty, objectStatusId }
            };

            foreach (var x in clipboard.Steps)
            {
                await SwapAsync(context, x, swap);
            }

            flow = await _connection.Filter<Flow>()
                .Eq(x => x.Id, flow.Id)
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Update
                .AddToSetEach(x => x.Steps, clipboard.Steps)
                .UpdateAndGetOneAsync();

            // TODO: FIRE EVENT
            // ...

            if (removeFromClipboard)
            {
                await _connection.Filter<Clipboard, FlowStepsClipboard>()
                    .Eq(x => x.AccountId, context.AccountId.Value)
                    .Eq(x => x.Id, clipboard.Id)
                    .Update
                    .Set(x => x.Operation, ClipboardOperation.Copy)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, context.Actor())
                    .Set(x => x.IsActive, false)
                    .UpdateOneAsync();

                // TODO: FIRE EVENT
                // ...
            }

            return Result.Success(flow);
        }
    }

    /// <summary>
    /// Swap step Id, event ids, current status id, ...
    /// </summary>
    private async ValueTask SwapAsync(IEntityContext context, FlowStep step, Dictionary<Guid, Guid?> swap)
    {
        // status
        if (swap.TryGetValue(step.CurrentStatusId ?? Guid.Empty, out var newStatusId) &&
            step.CurrentStatusId != newStatusId)
        {
            step.CurrentStatusId = newStatusId;
        }

        // outputs
        if (step.Options.Output?.Length > 0)
        {
            foreach (var output in step.Options.Output)
            {
                if (!output.EventId.HasValue) continue;
                if (!swap.TryGetValue(output.EventId.Value, out var newEventId))
                {
                    // create new event id
                    newEventId = Guid.NewGuid();
                    swap[output.EventId.Value] = newEventId;
                }

                output.EventId = newEventId;
            }
        }

        // step id
        var newStepId = Guid.NewGuid();
        swap[step.Id] = newStepId;
        step.Id = newStepId;

        // trigger 
        if (swap.TryGetValue(step.EventIdTrigger, out var newTriggerId) && newTriggerId.HasValue)
        {
            step.EventIdTrigger = newTriggerId.Value;
        }

        // allow builder to update internals
        var builder = await GetActionBuilderAsync(context, step.ActionId);
        await builder.SwapAsync(context, step, swap);
    }

    public async Task<(Flow Flow, FlowStep Step)> GetStepOrThrowAsync(IEntityContext context, Guid flowId, Guid stepId)
    {
        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .FirstOrDefaultAsync();

        if (flow == null) throw NotFoundException.New<Flow>(flowId);

        var step = flow.Steps.FirstOrDefault(x => x.Id == stepId);
        if (step == null) throw NotFoundException.New<FlowStep>(stepId);

        return (flow, step);
    }

    /// <summary>
    /// Edit Step (action) form
    /// </summary>
    public async Task<Form> GetEditStepFormAsync(IEntityContext context, Guid flowId, Guid stepId)
    {
        var (flow, step) = await GetStepOrThrowAsync(context, flowId, stepId);
        var builder = await GetActionBuilderAsync(context, step.ActionId);
        if (builder == null) throw new Exception("Invalid ActionId");

        var form = await builder.GetFormAsync(
            new FlowActionContext(context, flow)
            {
                EventTypeId = step.EventIdTrigger,
            },
            null,
            step
        );

        return form;
    }

    public async Task<Flow> AddStepAsync(IEntityContext context, Guid flowId, Guid eventTypeId, Guid actionId,
        Dictionary<string, object> requestParameters)
    {
        var builder = await GetActionBuilderAsync(context, actionId);
        if (builder == null) throw new Exception("Invalid ActionId");

        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .FirstOrDefaultAsync();

        if (flow == null) throw NotFoundException.New<Flow>(flowId);

        var step = await builder.AddOrUpdateStepAsync(context, flow, eventTypeId, requestParameters, null);
        if (step == null) return null;

        flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .Update
            .Push(x => x.Steps, step)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        // TODO: fire event
        // ...

        return flow;
    }

    public async Task<Flow> DeleteStepAsync(IContextWithActor context, Guid flowId, Guid stepId)
    {
        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .ElemMatchBuilder(x => x.Steps, q => q.Eq(x => x.Id, stepId))
            .Update
            .PullFilterBuilder(x => x.Steps, q => q.Eq(x => x.Id, stepId))
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        if (flow != null)
        {
            // TODO: fire event
            // ...
        }

        return flow;
    }

    public async Task<Flow> UpdateStepAsync(IEntityContext context, Guid flowId, Guid stepId,
        Dictionary<string, object> requestParameters)
    {
        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .FirstOrDefaultAsync();

        if (flow == null) throw NotFoundException.New<Flow>(flowId);

        var step = flow.Steps.FirstOrDefault(x => x.Id == stepId);
        if (step == null) throw NotFoundException.New<FlowStep>(stepId);

        var builder = await GetActionBuilderAsync(context, step.ActionId);
        if (builder == null) throw new Exception("Invalid ActionId");

        step = await builder.AddOrUpdateStepAsync(context, flow, step.EventIdTrigger, requestParameters, step);
        if (step == null) return null;

        flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .ElemMatchBuilder(x => x.Steps, q => q.Eq(x => x.Id, step.Id))
            .Update
            .Set($"{nameof(Flow.Steps)}.$", step)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        // TODO: fire event
        // ...

        return flow;
    }

    /// <summary>
    /// Calculate places holders available in the step
    /// </summary>
    public async Task<IEnumerable<Placeholder>> GetPlaceholdersAsync(IEntityContext context, Flow flow, Guid stepId)
    {
        var step = flow.Steps.FirstOrDefault(x => x.Id == stepId);

        var result = await GetPlaceholdersAsync(context, flow, step.CurrentStatusId, step.EventIdTrigger);

        var objectType = await _objectTypeService.GetAsync(context, flow.ObjectType);

        result = result.Concat(defaultPlaceholders());
        result = result.Concat(FlowEvent.GetDefaultEventPlaceHolders("InitialEvent"));

        return result;

        IEnumerable<Placeholder> defaultPlaceholders()
        {
            yield return new Placeholder
            {
                ObjectType = flow.ObjectType,
                Type = Placeholder.PlaceholderType.Object,
                Name = "{{Object}}",
                Description = objectType.Description ?? objectType.Name,
            };

            yield return new Placeholder
            {
                ObjectType = flow.ObjectType,
                Type = Placeholder.PlaceholderType.Object,
                Name = "{{InitialObject}}",
                Description = objectType.Description ?? objectType.Name,
            };

            yield return new Placeholder
            {
                ObjectType = flow.ObjectType,
                Type = Placeholder.PlaceholderType.Object,
                Name = "{{Objects." + FlowRun.GetObjectAlias(flow.ObjectType) + "}}",
                Description = objectType.Description ?? objectType.Name,
            };
        }
    }

    /// <summary>
    /// Calculate placeholders when handling event
    /// </summary>
    private async Task<IEnumerable<Placeholder>> GetPlaceholdersAsync(IEntityContext context, Flow flow, Guid? objectStatusId, Guid eventTypeId)
    {
        var triggers = flow
            .Steps
            .Where(x => x.CurrentStatusId == objectStatusId && x.Options?.Output != null &&
                        x.Options.Output.Any(x => x.EventId == eventTypeId))
            .ToArray();

        if (triggers.Length > 1)
        {
            Logger.LogInformation("More than one step fires the same {EventTypeId} in {FlowId}, use first", eventTypeId, flow.Id);
        }

        var trigger = triggers.FirstOrDefault();
        if (trigger == null)
        {
            // TODO: load the event and add properties from it (if it is user, system, ...)
            var evt = await _connection.Filter<EventType>()
                .In(x => x.AccountId, [context.AccountId.Value, AccountIds.CSS])
                .Eq(x => x.Id, eventTypeId)
                .FirstOrDefaultAsync();

            var triggerPlaceholders = evt?.Trigger switch
            {
                UserTrigger userTrigger => await GetPlaceholdersAsync(context, userTrigger),
                SystemTrigger systemTrigger => await GetPlaceholdersAsync(context, systemTrigger),
                ScheduledTrigger scheduledTrigger => await GetPlaceholdersAsync(context, scheduledTrigger),
                _ => Enumerable.Empty<Placeholder>(),
            };

            return triggerPlaceholders.Concat(FlowEvent.GetDefaultEventPlaceHolders());
        }

        var previous = await GetPlaceholdersAsync(context, flow, objectStatusId, trigger.EventIdTrigger);

        var builder = await GetActionBuilderAsync(context, trigger.ActionId);
        var result = await builder.GetPlaceholdersForOutputAsync(context, flow, trigger.Options, previous, eventTypeId);

        return result;
    }

    private ValueTask<IEnumerable<Placeholder>> GetPlaceholdersAsync(IEntityContext context, ScheduledTrigger scheduledTrigger)
    {
        return ValueTask.FromResult(Enumerable.Empty<Placeholder>());
    }

    private ValueTask<IEnumerable<Placeholder>> GetPlaceholdersAsync(IEntityContext context, SystemTrigger systemTrigger)
    {
        return ValueTask.FromResult(Enumerable.Empty<Placeholder>());
    }

    private ValueTask<IEnumerable<Placeholder>> GetPlaceholdersAsync(IEntityContext context, UserTrigger userTrigger)
    {
        if (userTrigger.Form?.Fields != null)
        {
            var list = userTrigger.Form.Fields.Select(x => x switch
            {
                ObjectField objectField => new Placeholder
                {
                    ObjectType = objectField.ObjectFieldOptions?.ObjectType,
                    Type = Placeholder.PlaceholderType.Object,
                    Name = "{{Event.MetaValues." + x.Name + "}}",
                    Description = x.Label ?? x.Name,
                },
                _ => new Placeholder
                {
                    ObjectType = x.GetBackingType().ValueType.ToString(),
                    Type = Placeholder.PlaceholderType.Value,
                    Name = "{{Event.MetaValues." + x.Name + "}}",
                    Description = x.Label ?? x.Name,
                }
            });

            return ValueTask.FromResult<IEnumerable<Placeholder>>(list);
        }

        return ValueTask.FromResult(Enumerable.Empty<Placeholder>());
    }
}