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
    }

    protected override async Task OnMessageAsync(IMessage message)
    {
        try
        {
            if (message.Body is SimpleActionMessage<FireWebhookActionOptions> webhook)
            {
                await CreateWebhookAsync(webhook);
            }

            // if (message.Body is GenericFlowEvent evt && message.RoutingKey.StartsWith("object."))
            // {
            //     await HandleAsync(message.RoutingKey, evt);
            // }
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

    private async Task CreateWebhookAsync(SimpleActionMessage<FireWebhookActionOptions> action)
    {
        using var scope = Logger.AddScope(new
        {
            action.Event.ObjectType,
            action.Event.TargetId,
        });

        Logger.LogInformation("Fire Webhook action");

        var evt = action.Event;
        var eventKey = $"{evt.ObjectType}_{action.Options.EventId}";

        var subscriptions = await _store.FindForDeliveryAsync(evt.AccountId, evt.ObjectType, eventKey);
        if (subscriptions.Count == 0) return;

        var accountContext = new AccountContext(evt.AccountId);
        var objectType = await _objectTypeService.GetAsync(accountContext, evt.ObjectType);
        if (objectType is null) return;

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