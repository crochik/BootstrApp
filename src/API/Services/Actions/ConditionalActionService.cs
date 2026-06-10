using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace Services;

public class ConditionalActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;

    public ConditionalActionService(
        ILogger<SetObjectStatusActionService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.Conditional));
        mapper.Register<ConditionalAction.Message>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var parts = evt.RoutingKey.Split('.');
            var eventId = Guid.Parse(parts[1]);

            switch (evt.Body)
            {
                case ConditionalAction.Message conditional:
                    await EvaluateConditionAsync(eventId, conditional);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }
    
    private async Task EvaluateConditionAsync(Guid eventId, ConditionalAction.Message action)
    {
        using var scope = Logger.AddScope(new
        {
            EventId = eventId,
            action.Event.ObjectType,
            ObjectId = action.Event.TargetId,
            action.Event.RunId,
        });

        Logger.LogInformation("Conditional Action");

        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.RunId)
            .IncludeFields(
                x => x.Objects,
                x => x.ObjectType,
                x => x.InitialEvent,
                x => x.InitialObject
            )
            .FirstOrDefaultAsync();

        // TODO: this probably has to evolve to handle coercing values to the field type
        //      it would require a value resolver that would have access to the object type 
        // ...

        var context = flowRun.BuildHandlebarsContext(action.Event);
        var result = false;
        var description = "No conditions";
        if (action.Options.Criteria?.Conditions != null)
        {
            result = true;
            description = "Satisfied all conditions";

            foreach (var condition in action.Options.Criteria.Conditions)
            {
                var fieldValue = context.ResolvePathValue(condition.FieldName);
                result = condition.EvaluateValue(fieldValue);

                Logger.LogInformation("Condition: {FieldValue} {Operator} with {Value}: {Result}", condition.FieldName,
                    condition.Operator, fieldValue, result);
                if (!result)
                {
                    description = $"Condition not met: {condition.FieldName} {condition.Operator} " +
                                  (fieldValue == null ? "{{NULL}}" : $"\"{fieldValue}\"");
                    break;
                }
            }
        }

        var fireEventId = result ? action.Options.TrueEventId : action.Options.FalseEventId;
        if (!fireEventId.HasValue) return;

        await MessageBroker.DispatchAsync(
            new GenericFlowEvent(action.Event)
            {
                Action = nameof(ActionIds.Conditional),
                Description = action.GetEventDescription(fireEventId, description),
                EventTypeId = fireEventId,
            }
        );
    }
}