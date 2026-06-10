using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Services.ActionRunners;

namespace PI.Shared.Services;

public class ActionRunnerFlowServiceOptions
{
    /// <summary>
    /// Actions to bind (register to receive events) 
    /// </summary>
    public Guid[] ActionIds { get; set; }
    
    /// <summary>
    /// Whether to try to process events locally (using registered runners)
    /// NOT IMPLEMENTED YET
    /// </summary>
    public bool TryToContinueLocally { get; set; }
}

/// <summary>
/// Process action events using runners
/// </summary>
public class ActionRunnerFlowService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly ActionRunnerFlowServiceOptions _options;
    private readonly Dictionary<Guid, IActionRunner> _runners;

    public ActionRunnerFlowService(ILogger<ActionRunnerFlowService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        IEnumerable<IActionRunner> runners,
        IOptions<ActionRunnerFlowServiceOptions> options
    ) :
        base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
        _options = options.Value;
        _runners = runners.ToDictionary(x => x.ActionId);
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        // subscribe to actions  handled by this service
        foreach (var actionId in _options.ActionIds)
        {
            if (!_runners.TryGetValue(actionId, out _))
            {
                Logger.LogCritical("{ActionId}: runner not registered", actionId);
                continue;
            }

            var route = ActionIds.GetRoute(actionId);
            Logger.LogInformation("Bind to {Route}", route);
            MessageBroker.Bind(messageQueue, route);
        }

        mapper.Register<SimpleActionMessage<GenericActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var parts = evt.RoutingKey.Split('.');
            var actionId = Guid.Parse(parts[1]);

            if (evt.Body is not IActionMessage action)
            {
                Logger.LogError("{Type}: is not for an action", evt.Body?.GetType().FullName);
                return;
            }

            if (!_runners.TryGetValue(actionId, out var runner))
            {
                Logger.LogError("{ActionId}: runner not registered", actionId);
                return;
            }

            var flowEvent = action.GetEvent();
            var accountContext = new AccountContext(flowEvent.AccountId);
            var objectType = await _objectTypeService.GetAsync(accountContext, flowEvent.ObjectType);
            var flowRun = await _connection.Filter<FlowRun>()
                .Eq(x => x.AccountId, flowEvent.AccountId)
                .Eq(x => x.Id, flowEvent.RunId)
                .IncludeFields(
                    x => x.Objects,
                    x => x.ObjectType,
                    x => x.InitialEvent,
                    x => x.InitialObject
                )
                .FirstOrDefaultAsync();

            using var scope = Logger.AddScope(new
            {
                ActionId = actionId,
                ActionRunner = runner.GetType().FullName,
                flowEvent.EventTypeId,
                flowEvent.ObjectType,
                flowEvent.TargetId,
                flowEvent.RunId,
                evt.RoutingKey
            });

            Logger.LogInformation("Run Action");

            var runnerContext = new ActionRunnerContext
            {
                Event = flowEvent,
                ObjectId = flowEvent.TargetId,
                ObjectType = objectType,
                EntityContext = accountContext,
                Run = flowRun,
            };

            var nextEvents = await runner.RunAsync(runnerContext, action.GetActionOptions());
            if (nextEvents == null)
            {
                Logger.LogInformation("Runner returned no events");
                return;
            }

            foreach (var nextEvent in nextEvents)
            {
                if (_options.TryToContinueLocally)
                {
                    // TODO: could check whether it can other runners here w/o having to fire events
                    // copy/share code from ActionRunnerService
                    // ...
                }

                Logger.LogInformation("Fire Event: {NextEventTypeId}", nextEvent.EventTypeId);
                await MessageBroker.DispatchAsync(nextEvent);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {Id}", evt.RoutingKey);
        }
        finally
        {
            evt.Acknowledge();
        }
    }
}