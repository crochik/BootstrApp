using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Integrations.Subscriptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.Shared.Integrations.Delivery;

public sealed class WebhookEventListener : AbstractMessageQueueService, ILifetimeService
{
    private readonly ObjectTypeService _objectTypeService;
    private readonly ISubscriptionStore _store;
    private readonly IEventPublisher _publisher;

    public WebhookEventListener(
        ILogger<WebhookEventListener> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        ISubscriptionStore store,
        IEventPublisher publisher)
        : base(logger, configuration, messageBroker)
    {
        _objectTypeService = objectTypeService;
        _store = store;
        _publisher = publisher;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        // Every object lifecycle event for every object type: object.{type}.{id}.{action}
        // MessageBroker.Bind(queue, "object.#");
        // mapper.Register<GenericFlowEvent>();

        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.FireWebhook));
        mapper.Register<SimpleActionMessage<FireWebhookActionOptions>>();
        mapper.Register<SimpleActionMessage<GenericActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage message)
    {
        try
        {
            Logger.LogInformation("Processing object event {RoutingKey}", message.RoutingKey);
            switch (message.Body)
            {
                case SimpleActionMessage<GenericActionOptions> msg:
                    await ProcessMessageAsync(msg);
                    break;

                case SimpleActionMessage<FireWebhookActionOptions> msg:
                    await CreateWebhookAsync(msg.Event, msg.Options);
                    break;
                
                default:
                    Logger.LogError("Unexpected Message: {Type}", message.Body.GetType());
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process object event {RoutingKey}", message.RoutingKey);
        }
        finally
        {
            message.Acknowledge();
        }
    }

    private async Task ProcessMessageAsync(SimpleActionMessage<GenericActionOptions> action)
    {
        if (action.Options is not GenericActionOptions genericActionOptions)
        {
            Logger.LogError("Unexpected Options");
            return;
        }
        
        var options = genericActionOptions.ConvertTo<FireWebhookActionOptions>();
        options.Output = genericActionOptions.Output;

        await CreateWebhookAsync(action.Event, options);
    }

    private async Task CreateWebhookAsync(FlowEvent evt, FireWebhookActionOptions options)
    {
        using var scope = Logger.AddScope(new
        {
            evt.ObjectType,
            evt.TargetId,
        });

        Logger.LogInformation("Fire Webhook action");

        var eventKey = $"{evt.ObjectType}_{options.EventId}";

        var subscriptions = await _store.FindForDeliveryAsync(evt.AccountId, evt.ObjectType, eventKey);
        if (subscriptions.Count == 0)
        {
            Logger.LogInformation("Found no subscriptions in the {Account} for {ObjectType}_{EventKey}", evt.AccountId, evt.ObjectType, eventKey);
            return;
        }

        var accountContext = new AccountContext(evt.AccountId);
        var objectType = await _objectTypeService.GetAsync(accountContext, evt.ObjectType);
        if (objectType is null)
        {
            Logger.LogError("Couldn't load {ObjectType} for {AccountId}", evt.ObjectType, evt.AccountId);
            return;
        }

        foreach (var subscription in subscriptions)
        {
            var profileContext = ProfileContext.Create(
                subscription.ProfileId,
                evt.AccountId,
                subscription.EntityId, /* user id */
                subscription.ClientId,
                subscription.OrganizationId
            );

            var flat = await _objectTypeService.GetFlatObjectAsync(profileContext, objectType, evt.TargetId);
            if (flat is null)
            {
                // "profile" doesn't have access to this object, skip
                Logger.LogInformation("{ProfileId} {UserId} {OrganizationId} does not have access to {ObjectType} {ObjectId}",
                    subscription.ProfileId,
                    subscription.EntityId,
                    subscription.OrganizationId,
                    evt.ObjectType,
                    evt.TargetId
                    );
                continue;
            }

            await _publisher.PublishAsync(
                new WebhookEventData(evt.AccountId, evt.ObjectType, eventKey, flat),
                [subscription]
            );

            Logger.LogInformation("Published {ObjectType}/{Event} to subscription for profile {ProfileId}",
                evt.ObjectType, eventKey, subscription.ProfileId);
        }
    }
}