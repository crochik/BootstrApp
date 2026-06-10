using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using RabbitMQ.Client;
using Webhook.Publisher.Messaging;
using Webhook.Publisher.Storage;
using Webhook.Publisher.Subscriptions;

namespace Webhook.Publisher.Publishing;

/// <summary>
/// Default <see cref="IWebhookPublisher"/>. Persists the event + per-subscription
/// deliveries to MongoDB (the source of truth), then enqueues one lightweight
/// reference message per delivery on the topic exchange using a pooled
/// publisher-confirm channel.
/// </summary>
public sealed class RabbitMqWebhookPublisher : IWebhookPublisher
{
    private readonly IWebhookEventStore _eventStore;
    private readonly IWebhookSubscriptionStore _subscriptions;
    private readonly IWebhookConnectionManager _connectionManager;
    private readonly WebhookTopologyNames _names;
    private readonly ILogger<RabbitMqWebhookPublisher> _logger;

    public RabbitMqWebhookPublisher(
        IWebhookEventStore eventStore,
        IWebhookSubscriptionStore subscriptions,
        IWebhookConnectionManager connectionManager,
        WebhookTopologyNames names,
        ILogger<RabbitMqWebhookPublisher> logger)
    {
        _eventStore = eventStore;
        _subscriptions = subscriptions;
        _connectionManager = connectionManager;
        _names = names;
        _logger = logger;
    }

    public async Task<PublishResult> PublishAsync(string tenantId, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(payload);

        var now = DateTime.UtcNow;
        var eventId = Guid.NewGuid().ToString("n");

        var webhookEvent = new WebhookEvent
        {
            Id = eventId,
            TenantId = tenantId,
            EventName = eventName,
            OccurredAt = now,
            Payload = ToBsonDocument(payload),
            CreatedAt = now,
        };
        await _eventStore.SaveEventAsync(webhookEvent, cancellationToken);

        var subscriptions = await _subscriptions.GetForAsync(tenantId, eventName, cancellationToken);
        if (subscriptions.Count == 0)
        {
            _logger.LogInformation("Event {EventId} for {TenantId}/{EventName} has no matching subscriptions", eventId, tenantId, eventName);
            return new PublishResult(eventId, 0, true);
        }

        var deliveries = subscriptions.Select(sub => new WebhookDelivery
        {
            Id = Guid.NewGuid().ToString("n"),
            EventId = eventId,
            TenantId = tenantId,
            EventName = eventName,
            SubscriptionId = sub.Id,
            Url = sub.Url,
            Secret = sub.Secret,
            SignatureHeader = sub.SignatureHeader,
            Status = DeliveryStatus.Pending,
            NextAttemptAt = now, // due immediately; gives the reconciler a uniform sweep key
            CreatedAt = now,
            UpdatedAt = now,
        }).ToList();

        await _eventStore.CreateDeliveriesAsync(deliveries, cancellationToken);

        var routingKey = RoutingKey.For(tenantId, eventName).Value;
        var allAcknowledged = await EnqueueAsync(deliveries, routingKey, cancellationToken);

        return new PublishResult(eventId, deliveries.Count, allAcknowledged);
    }

    private async Task<bool> EnqueueAsync(IReadOnlyList<WebhookDelivery> deliveries, string routingKey, CancellationToken cancellationToken)
    {
        var channel = await _connectionManager.RentPublishChannelAsync(cancellationToken);
        var allAcknowledged = true;
        try
        {
            foreach (var delivery in deliveries)
            {
                var body = new DeliveryMessage(delivery.Id).Serialize();
                var props = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    MessageId = delivery.Id,
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                };

                try
                {
                    await channel.BasicPublishAsync(_names.DeliveryExchange, routingKey, mandatory: true, props, body, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Publisher confirm nacked/timed out; the reconciler will re-enqueue this Pending delivery.
                    allAcknowledged = false;
                    _logger.LogWarning(ex, "Broker did not confirm delivery {DeliveryId}; left Pending for reconciler", delivery.Id);
                }
            }
        }
        finally
        {
            await _connectionManager.ReturnPublishChannelAsync(channel);
        }

        return allAcknowledged;
    }

    private static BsonDocument ToBsonDocument(object payload)
    {
        // Round-trip via System.Text.Json so anonymous types / POCOs serialize with
        // the same conventions used on the delivery side, and the stored document is
        // queryable in MongoDB.
        if (payload is BsonDocument alreadyBson)
        {
            return alreadyBson;
        }

        var json = payload as string ?? JsonSerializer.Serialize(payload);
        return BsonDocument.Parse(json);
    }
}
