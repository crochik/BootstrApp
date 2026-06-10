using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Crochik.Messaging;
using Crochik.Mongo;
using FlowActions;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Models;

namespace PI.Shared.Services.ActionRunners;

public class ActionRunnerService
{
    private readonly Dictionary<Guid, IActionRunner> _runners;
    private readonly ILogger<ActionRunnerService> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly IMessageBroker _messageBroker;
    private readonly Dictionary<Guid, IFlowActionBuilder> _builders;

    public ActionRunnerService(
        ILogger<ActionRunnerService> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        IMessageBroker messageBroker,
        IEnumerable<IActionRunner> runners,
        IEnumerable<IFlowActionBuilder> builders
    )
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _messageBroker = messageBroker;

        _runners = runners.ToDictionary(x => x.ActionId);
        _builders = builders.ToDictionary(b => b.Id);
    }

    /// <summary>
    /// Start Execution 
    /// </summary>
    public async Task RunAsync(IEntityContext context, FlowEvent initialEvent)
    {
        var objectTypeName = initialEvent.ObjectType;
        var objectId = initialEvent.TargetId;

        var objectType = await _objectTypeService.GetAsync(context, objectTypeName);
        if (objectType == null) throw NotFoundException.New(objectTypeName);

        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, initialEvent.FlowId)
            .FirstOrDefaultAsync();

        await ReloadObjectAndProcessEventAsync(context, objectType, objectId, flow, initialEvent);
    }

    /// <summary>
    /// Start Execution with a lot more data :) 
    /// </summary>
    public async Task ProcessEventAsync(
        IEntityContext context, ObjectType objectType, Guid objectId, Dictionary<string, object> flatObject, Flow flow, FlowEvent triggerEvent,
        Channel<IResult> channel = null, CancellationToken ct = default
    )
    {
        // TODO: ADD APM 
        // ...

        if (!flatObject.TryGetGuidParam(nameof(FlowObjectModel.ObjectStatusId), out var objectStatusId))
        {
            throw new BadRequestException("Couldn't find object status id");
        }

        var steps = flow.Steps
            .Where(x => (!x.CurrentStatusId.HasValue || x.CurrentStatusId == objectStatusId) && x.EventIdTrigger == triggerEvent.EventTypeId)
            .ToArray();

        if (steps.Length < 1)
        {
            // TODO: should copy behavior of the flowservice ?
            // e.g. add event as final event instead of step?
            // ...
        }

        if (channel != null) await channel.Writer.WriteAsync(Result.Unknown("Initiating Flow Run"), ct);

        var flowRun = await UpsertFlowRunAsync(context, steps, flatObject, triggerEvent);

        if (steps.Length < 1)
        {
            if (channel != null) await channel.Writer.WriteAsync(Result.Unknown("Nothing to do"), ct);
            return;
        }

        // in series
        if (channel != null) await channel.Writer.WriteAsync(Result.Unknown($"Found {steps.Length} Steps"), ct);
        foreach (var step in steps)
        {
            await processStepAsync(step);
        }

        if (channel != null) await channel.Writer.WriteAsync(Result.Unknown("Finished Flow"), ct);
        return;

        async Task processStepAsync(FlowStep step)
        {
            if (!_runners.TryGetValue(step.ActionId, out var actionRunner))
            {
                _logger.LogInformation("Can't run {ActionId} locally, fire event", step.ActionId);
                if (channel != null) await channel.Writer.WriteAsync(Result.Unknown($"{step.Description}: can not run locally, fire event"), ct);
                await FireActionMessageAsync(context, step, triggerEvent);
                return;
            }

            var start = DateTime.UtcNow;

            var runnerContext = new ActionRunnerContext
            {
                ObjectId = objectId,
                EntityContext = context,
                ObjectType = objectType,
                Run = flowRun,
                Event = triggerEvent,
            };

            var events = (await actionRunner.RunAsync(runnerContext, step.Options))?
                .Where(x => x.EventTypeId.HasValue)
                .ToArray();

            _logger.LogInformation("Processed {Action} in {Elapsed} ms", actionRunner.GetType().Name, (DateTime.UtcNow - start).TotalMilliseconds);

            if (events == null) return;

            await processEvents(events);
        }

        async Task processEvents(FlowEvent[] events)
        {
            // run in parallel
            // if (events.Length == 1)
            // {
            //     await ProcessEventAsync(context, objectType, objectId, flow, events[0]);
            //     continue;
            // }
            //
            // var tasks = events.Select(evt => ProcessEventAsync(context, objectType, objectId, flow, evt));
            // await Task.WhenAll(tasks);

            // run in series
            foreach (var evt in events)
            {
                await ReloadObjectAndProcessEventAsync(context, objectType, objectId, flow, evt, channel, ct);
            }
        }
    }

    private async Task ReloadObjectAndProcessEventAsync(IEntityContext context, ObjectType objectType, Guid objectId, Flow flow, FlowEvent evt,
        Channel<IResult> channel = null, CancellationToken ct = default
    )
    {
        var obj = await _objectTypeService.GetFlatObjectAsync(context, objectType, objectId);
        if (obj == null) throw NotFoundException.New(objectType.Name, objectId);

        if (flow.Id != evt.FlowId)
        {
            _logger.LogError("Event is not for the flow");
            return;
        }

        var flowIds = objectType.GetFlowIds(obj).ToArray();
        if (flowIds.All(x => x != evt.FlowId))
        {
            _logger.LogError("  Object Type flow doesn't match event");
            return;
        }

        await ProcessEventAsync(context, objectType, objectId, obj, flow, evt, channel, ct);
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

    private async Task FireActionMessageAsync(IEntityContext accountContext, FlowStep step, FlowEvent flowEvent)
    {
        var builder = await GetActionBuilderAsync(accountContext, step.ActionId);
        if (builder == null)
        {
            _logger.LogWarning("Unknown {ActionId}", step.ActionId);
            return;
        }

        try
        {
            var (message, route) = builder.Build(accountContext, flowEvent, step.Options);
            if (message == null)
            {
                _logger.LogWarning("{Action} => failed", builder.GetType().Name);
                return;
            }

            _logger.LogInformation(
                "{Action} => {Route}",
                builder.GetType().Name,
                route
            );

            await _messageBroker.PublishAsync(route, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger action");
        }
    }

    private async Task<FlowRun> UpsertFlowRunAsync(IEntityContext accountContext, FlowStep[] steps, Dictionary<string, object> flatObject, FlowEvent flowEvent)
    {
        // get event
        var eventType = await _connection.Filter<EventType>()
            .Eq(x => x.Id, flowEvent.EventTypeId)
            .FirstOrDefaultAsync();

        // user event? 
        var loadedObjects = default(Dictionary<string, ObjectWithType>);
        if (eventType?.Trigger is UserTrigger userTrigger && userTrigger.RelatedObjects != null && flowEvent.Actor is AbstractAPIActor apiActor)
        {
            // load related objects

            // THIS WAS THE PREVIOUS ATTEMPT BUT WOULD ENFORCE THAT THE USER HAS ACCESS TO THE RELATION 
            // BUT IT WOULD NOT TAKE INTO ACCOUNT THE PROFILE - would obviously fail for OTG for example
            // ... 
            // var user = apiActor.UserId.HasValue
            //     ? await _connection.Filter<Entity, User>()
            //         .Eq(x => x.AccountId, accountContext.AccountId)
            //         .Eq(x => x.Id, apiActor.UserId.Value)
            //         .Ne(x => x.IsActive, false)
            //         .FirstOrDefaultAsync()
            //     : null;
            // var loaded = await _objectTypeService.LoadRelatedObjectsAsync(user?.Context ?? accountContext, userTrigger, flowEvent.ObjectType, flatObject);

            var loaded = await _objectTypeService.LoadRelatedObjectsAsync(accountContext, userTrigger, flowEvent.ObjectType, flatObject);
            if (!loaded.IsSuccess)
            {
                _logger.LogError("Couldn't load related objects: {Error}", loaded.Status);
                return null;
            }

            loadedObjects = loaded.Value;
        }

        return await _objectTypeService.UpsertAndGetFlowRunAsync(flatObject, flowEvent, steps, loadedObjects);
    }
}