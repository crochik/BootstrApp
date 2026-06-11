using Crochik.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PI.Shared.Integrations.Subscriptions;

namespace PI.Shared.Integrations.Delivery;

/// <summary>The event data to publish: the flattened object body plus its identity.</summary>
public sealed record WebhookEventData(Guid AccountId, string ObjectType, string EventKey, IDictionary<string, object> Payload);

/// <summary>
/// Emits an event to a resolved set of subscriptions. The event listener calls this
/// after it has matched subscriptions and built the per-subscriber payload. Delivery is
/// durable and asynchronous: the event and one delivery per subscription are stored,
/// then each delivery reference is enqueued for a signed, retried HTTP POST.
/// </summary>
public interface IEventPublisher
{
    /// <returns>The number of per-subscription deliveries enqueued.</returns>
    Task<WebhookDelivery[]> PublishAsync(WebhookEventData data, IReadOnlyList<IntegrationSubscription> targets, CancellationToken ct = default);
}

/// <summary>
/// Stores the event + per-subscription deliveries (MongoDB, the source of truth) and
/// enqueues one lightweight reference message per delivery on the broker. The delivery
/// worker then signs and POSTs; the reconciler is the safety net for anything not
/// enqueued.
/// </summary>
public sealed class WebhookEventPublisher : IEventPublisher
{
    private readonly IWebhookStore _store;
    private readonly IMessageBroker _broker;
    private readonly DeliveryOptions _options;
    private readonly ILogger<WebhookEventPublisher> _logger;

    public WebhookEventPublisher(IWebhookStore store, IMessageBroker broker, IOptions<DeliveryOptions> options, ILogger<WebhookEventPublisher> logger)
    {
        _store = store;
        _broker = broker;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WebhookDelivery[]> PublishAsync(WebhookEventData data, IReadOnlyList<IntegrationSubscription> targets, CancellationToken ct = default)
    {
        if (targets.Count == 0) return [];

        var now = DateTime.UtcNow;
        var webhookEvent = new WebhookEvent
        {
            Id = Guid.NewGuid(),
            AccountId = data.AccountId,
            ObjectType = data.ObjectType,
            EventKey = data.EventKey,
            EventName = $"{data.ObjectType}.{data.EventKey}",
            Payload = data.Payload as Dictionary<string, object> ?? new Dictionary<string, object>(data.Payload),
            OccurredAt = now,
            CreatedOn = now,
        };
        await _store.SaveEventAsync(webhookEvent);

        var deliveries = targets.Select(sub => new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            EventId = webhookEvent.Id,
            AccountId = data.AccountId,
            EventName = webhookEvent.EventName,
            SubscriptionId = sub.Id,
            Url = sub.Url,
            Secret = sub.Secret,
            SignatureHeader = sub.SignatureHeader ?? "Webhook-Signature",
            Status = DeliveryStatus.Pending,
            NextAttemptAt = now, // due immediately; gives the reconciler a uniform sweep key
            CreatedOn = now,
            UpdatedOn = now,
        }).ToArray();

        await _store.CreateDeliveriesAsync(deliveries);

        foreach (var delivery in deliveries)
        {
            try
            {
                await _broker.PublishAsync(_options.DeliveryTopic, new DeliveryDispatch { DeliveryId = delivery.Id });
            }
            catch (Exception ex)
            {
                // Left Pending; the reconciler re-enqueues it on its next sweep.
                _logger.LogWarning(ex, "Failed to enqueue delivery {DeliveryId}; left for reconciler", delivery.Id);
            }
        }

        return deliveries;
    }
}

/// <summary>
/// The entire wire payload: just a reference to the delivery whose state lives in
/// MongoDB. No event payload travels through the broker.
/// </summary>
public sealed class DeliveryDispatch : IMessageBody
{
    public Guid DeliveryId { get; set; }
}
