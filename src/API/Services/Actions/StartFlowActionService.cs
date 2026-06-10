using System.Dynamic;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;

namespace Services;

public class StartFlowActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly ObjectTypeService _objectTypeService;

    public StartFlowActionService(
        ILogger<SetObjectStatusActionService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        ObjectTypeService objectTypeService
    ) : base(logger, configuration, messageBroker)
    {
        _objectTypeService = objectTypeService;
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.StartFlow));
        mapper.Register<SimpleActionMessage<StartFlowActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var parts = evt.RoutingKey.Split('.');
            var eventId = Guid.Parse(parts[1]);

            switch (evt.Body)
            {
                case SimpleActionMessage<StartFlowActionOptions> startFlow:
                    await StartFlowAsync(eventId, startFlow);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }
    
    private async Task StartFlowAsync(Guid eventId, SimpleActionMessage<StartFlowActionOptions> action)
    {
        using var scope = Logger.AddScope(new
        {
            EventId = eventId,
            action.Event.ObjectType,
            ObjectId = action.Event.TargetId,
            action.Event.RunId,
            action.Options.FieldName,
        });

        Logger.LogInformation("Start Flow Action");

        var result = await StartFlowAsync(action);

        var fireEventId = result.IsSuccess ? action.Options.NextEventId : action.Options.AlreadyRunningEventId;
        if (!fireEventId.HasValue) return;

        await MessageBroker.DispatchAsync(
            new GenericFlowEvent(action.Event)
            {
                Action = nameof(ActionIds.StartFlow),
                Description = action.GetEventDescription(fireEventId,
                    result.IsSuccess ? result.Status ?? "Flow initiated" : result.Status),
                EventTypeId = fireEventId,
            }
        );
    }

    private async Task<Result<ExpandoObject>> StartFlowAsync(SimpleActionMessage<StartFlowActionOptions> action)
    {
        var context = new AccountContext(action.Event.AccountId);
        var objectType = await _objectTypeService.GetAsync(context, action.Event.ObjectType);
        if (objectType == null) throw new NotFoundException($"{action.Event.ObjectType} not found");

        var obj = await _objectTypeService.GetExpandoObjectByIdAsync(context, objectType, action.Event.TargetId);
        if (obj == null) throw new NotFoundException(objectType.Name, action.Event.TargetId);

        if (!objectType.Fields.TryGetValue(action.Options.FieldName, out var field))
            throw new NotFoundException($"{action.Options.FieldName} not found");

        var value = obj.GetFieldValue(action.Options.FieldName);
        switch (field.Field)
        {
            case ReferenceField referenceField:
                var currValue = value switch
                {
                    string str => Guid.TryParse(str, out var id) ? id : default(Guid?),
                    Guid id => id,
                    _ => default(Guid?)
                };

                if (currValue.HasValue && currValue.Value == action.Options.FlowId)
                    return Result.Unknown<ExpandoObject>("Already running");

                obj = await _objectTypeService.UpdateObjectAsync(
                    context,
                    objectType,
                    action.Event.TargetId,
                    q => q
                        .Ne(field.Field.GetPathInCollection(), action.Options.FlowId)
                        .Update
                        .Set(field.Field.GetPathInCollection(), action.Options.FlowId),
                    new Dictionary<string, object>
                    {
                        { field.Field.Name, action.Options.FlowId }
                    }
                );

                return obj == null ? Result.Unknown<ExpandoObject>("Already set") : Result.Success(obj);

            case MultiReferenceField multiReferenceField:
                // TODO: ...
                break;

            default:
                throw new BadRequestException("Invalid field type");
        }

        return Result.Success(obj);
    }
}