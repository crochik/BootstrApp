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

public class TagObjectActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly ObjectTypeService _objectTypeService;

    public TagObjectActionService(
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
        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.TagObject));
        mapper.Register<SimpleActionMessage<TagObjectActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var parts = evt.RoutingKey.Split('.');
            var eventId = Guid.Parse(parts[1]);

            switch (evt.Body)
            {
                case SimpleActionMessage<TagObjectActionOptions> tagObject:
                    await TagObjectAsync(eventId, tagObject);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }
    
    private async Task TagObjectAsync(Guid eventId, SimpleActionMessage<TagObjectActionOptions> action)
    {
        using var scope = Logger.AddScope(new
        {
            EventId = eventId,
            action.Event.ObjectType,
            ObjectId = action.Event.TargetId,
            action.Event.RunId,
            action.Options.FieldName,
            action.Options.Tag
        });

        Logger.LogInformation("Tag Object Action");

        var result = await TagObjectAsync(action);

        var fireEventId = result.IsSuccess ? action.Options.NextEventId : action.Options.AlreadyTaggedEventId;
        if (!fireEventId.HasValue) return;

        await MessageBroker.DispatchAsync(
            new GenericFlowEvent(action.Event)
            {
                Action = nameof(ActionIds.TagObject),
                Description = action.GetEventDescription(fireEventId,
                    result.IsSuccess ? (result.Status ?? "Object tagged") : result.Status),
                EventTypeId = fireEventId,
            }
        );
    }

    /// <summary>
    /// Tag object (set checkbox or add tag) if that is not already the state
    /// Return unknown if object it is already the state
    /// </summary>
    private async Task<Result<ExpandoObject>> TagObjectAsync(SimpleActionMessage<TagObjectActionOptions> action)
    {
        var context = new AccountContext(action.Event.AccountId);
        var objectType = await _objectTypeService.GetAsync(context, action.Event.ObjectType);
        if (objectType == null) throw new NotFoundException($"{action.Event.ObjectType} not found");

        var obj = await _objectTypeService.GetExpandoObjectByIdAsync(context, objectType, action.Event.TargetId);
        if (obj == null) throw new NotFoundException(objectType.Name, action.Event.TargetId);

        if (!objectType.Fields.TryGetValue(action.Options.FieldName, out var field))
            throw new NotFoundException($"{action.Options.FieldName} not found");
        switch (field.Field)
        {
            case CheckboxField:
            {
                if (obj.TryGetFieldValue(field.Field.Name, out var value))
                {
                    var set = value switch
                    {
                        bool bit => bit,
                        string str => bool.TryParse(str, out var bit) && bit,
                        null => false,
                        _ => throw new Exception($"Unexpected field value for checkbox: {value.GetType().FullName}")
                    };

                    if (set)
                    {
                        Logger.LogInformation("Checkbox is already checked");
                        return Result.Unknown<ExpandoObject>("Checkbox field is already true");
                    }
                }

                Logger.LogInformation("set field value to true");

                obj = await _objectTypeService.UpdateObjectAsync(
                    context,
                    objectType,
                    action.Event.TargetId,
                    q => q
                        .Ne(field.Field.GetPathInCollection(), true)
                        .Update
                        .Set(field.Field.GetPathInCollection(), true),
                    new Dictionary<string, object>
                    {
                        { field.Field.Name, true }
                    }
                );

                break;
            }

            case TagsField:
            {
                if (obj.TryGetFieldValue(field.Field.Name, out var value) && value != null)
                {
                    var tags = value switch
                    {
                        string[] objs => objs,
                        IEnumerable<string> objs => objs.ToArray(),
                        IEnumerable<object> objs => objs.OfType<string>().ToArray(),
                        _ => throw new Exception($"Unexpected field value for tags: {value.GetType().FullName}")
                    };

                    if (tags.Any(x => string.Equals(x, action.Options.Tag)))
                    {
                        Logger.LogInformation("Object already tagged");
                        return Result.Unknown<ExpandoObject>("Object already tagged");
                    }
                }

                Logger.LogInformation("Add tag to field");

                obj = await _objectTypeService.UpdateObjectAsync(
                    context,
                    objectType,
                    action.Event.TargetId,
                    q => q
                        .Ne(field.Field.GetPathInCollection(), action.Options.Tag)
                        .Update
                        .AddToSet(field.Field.GetPathInCollection(), action.Options.Tag),
                    new Dictionary<string, object>
                    {
                        { field.Field.Name, "[...]" }
                    }
                );
                break;
            }

            default:
                throw new BadRequestException("Invalid field type");
        }

        if (obj == null) return Result.Unknown<ExpandoObject>("Update failed");

        return Result.Success(obj);
    }
    
}