using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class SetObjectStatusActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public SetObjectStatusActionService(
        ILogger<SetObjectStatusActionService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        MongoConnection connection,
        ObjectTypeService objectTypeService
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.SetObjectStatus));
        mapper.Register<SetObjectStatusAction.Message>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var parts = evt.RoutingKey.Split('.');
            var eventId = Guid.Parse(parts[1]);

            switch (evt.Body)
            {
                case SetObjectStatusAction.Message change:
                    await SetObjectStatusAsync(eventId, change);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }
    
        private async Task SetObjectStatusAsync(Guid eventId, SetObjectStatusAction.Message action)
    {
        var evt = action.Event;
        var accountContext = new AccountContext(evt.AccountId).With(evt.Actor);

        using var scope = Logger.AddScope(new
        {
            evt.AccountId,
            evt.FlowId,
            evt.TargetId,
            evt.ObjectType,
            evt.StatusId,
            evt.RunId,
            EventTypeId = eventId,
        });

        var status = await _connection.Filter<ObjectStatus>()
            .Eq(x => x.AccountId, accountContext.AccountId.Value)
            .Eq(x => x.ObjectType, evt.ObjectType)
            .Eq(x => x.Id, action.Options.ObjectStatusId)
            .FirstOrDefaultAsync();

        if (status == null)
        {
            Logger.LogError("{ObjectStatusId} not found", action.Options.ObjectStatusId);
            throw new NotFoundException(nameof(ObjectStatus), action.Options.ObjectStatusId);
        }

        Logger.LogInformation("Change Status to {ObjectStatusId}: {ObjectStatus}", action.Options.ObjectStatusId,
            status.Name);

        var statusUpdated = await _objectTypeService.UpdateObjectStatusAsync(accountContext, evt.ObjectType,
            evt.TargetId, action.Options.ObjectStatusId.Value);

        if (action.Options.NextEventId.HasValue)
        {
            // old behavior, fire next event id regardless of whether it actually changed or not
            await MessageBroker.DispatchAsync(
                new GenericFlowEvent(action.Event)
                {
                    Action = nameof(ActionIds.SetObjectStatus),
                    Description =
                        action.GetEventDescription(action.Options.NextEventId, $"Status updated to {status.Name}"),
                    EventTypeId = action.Options.NextEventId,
                }
            );
        }

        if (statusUpdated)
        {
            // only fire if it actually changed 
            await MessageBroker.DispatchAsync(
                new GenericFlowEvent(action.Event)
                {
                    Action = nameof(ActionIds.SetObjectStatus),
                    Description = $"Status updated to {status.Name}",
                    EventTypeId = EventIds.OnStatusEntered,
                }
            );
        }
    }
}