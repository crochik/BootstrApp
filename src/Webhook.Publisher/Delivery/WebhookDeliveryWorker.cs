using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Webhook.Publisher.Configuration;
using Webhook.Publisher.Messaging;
using Webhook.Publisher.Storage;

namespace Webhook.Publisher.Delivery;

/// <summary>
/// Consumes delivery references from the main queue, delivers them over HTTP and,
/// on retryable failure, reschedules them through the tiered delay ("wait room")
/// queues. MongoDB is the source of truth for status and attempt count.
/// </summary>
public sealed class WebhookDeliveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebhookEventStore _store;
    private readonly IWebhookConnectionManager _connectionManager;
    private readonly WebhookPublisherOptions _options;
    private readonly WebhookTopologyNames _names;
    private readonly ILogger<WebhookDeliveryWorker> _logger;
    private readonly List<IChannel> _channels = new();

    public WebhookDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        IWebhookEventStore store,
        IWebhookConnectionManager connectionManager,
        IOptions<WebhookPublisherOptions> options,
        WebhookTopologyNames names,
        ILogger<WebhookDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _store = store;
        _connectionManager = connectionManager;
        _options = options.Value;
        _names = names;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await _connectionManager.GetConnectionAsync(stoppingToken);
        var delivery = _options.Delivery;

        for (var i = 0; i < Math.Max(1, delivery.ConsumerCount); i++)
        {
            var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: delivery.PrefetchCount, global: false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, ea) => HandleAsync(channel, ea, stoppingToken);
            await channel.BasicConsumeAsync(_names.DeliveryQueue, autoAck: false, consumer, stoppingToken);
            _channels.Add(channel);
        }

        _logger.LogInformation("Webhook delivery worker started with {ConsumerCount} consumer(s)", _channels.Count);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var message = DeliveryMessage.Deserialize(ea.Body.Span);
        if (message is null)
        {
            _logger.LogError("Dropping unparseable delivery message (routingKey {RoutingKey})", ea.RoutingKey);
            await SafeAckAsync(channel, ea.DeliveryTag);
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var staleBefore = now - _options.Delivery.ClaimTimeout;
            var delivery = await _store.TryMarkDeliveringAsync(message.DeliveryId, now, staleBefore, stoppingToken);
            if (delivery is null)
            {
                // Already claimed, delivered or terminal — duplicate. Idempotent no-op.
                await SafeAckAsync(channel, ea.DeliveryTag);
                return;
            }

            var attemptsMade = delivery.AttemptCount + 1;
            var firstAttemptAt = delivery.FirstAttemptAt ?? now;

            var webhookEvent = await _store.GetEventAsync(delivery.EventId, stoppingToken);
            if (webhookEvent is null)
            {
                _logger.LogError("Event {EventId} for delivery {DeliveryId} not found; marking Failed", delivery.EventId, delivery.Id);
                var missing = new DeliveryAttempt
                {
                    Number = attemptsMade,
                    At = now,
                    Outcome = DeliveryOutcomeKind.PermanentFailure,
                    Error = "event not found",
                };
                await _store.RecordAttemptAsync(delivery.Id, missing, DeliveryStatus.Failed, null, stoppingToken);
                await SafeAckAsync(channel, ea.DeliveryTag);
                return;
            }

            DeliveryResult result;
            using (var scope = _scopeFactory.CreateScope())
            {
                var client = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryClient>();
                result = await client.DeliverAsync(delivery, webhookEvent, stoppingToken);
            }

            var decision = RetrySchedule.Decide(attemptsMade, firstAttemptAt, now, result.Outcome, _options.Retry);
            var attempt = new DeliveryAttempt
            {
                Number = attemptsMade,
                At = now,
                StatusCode = result.StatusCode,
                Outcome = ToKind(result.Outcome),
                Error = result.Error,
                DurationMs = result.DurationMs,
            };

            switch (decision.Action)
            {
                case DeliveryAction.Complete:
                    await _store.RecordAttemptAsync(delivery.Id, attempt, DeliveryStatus.Delivered, null, stoppingToken);
                    break;

                case DeliveryAction.Fail:
                    _logger.LogWarning("Delivery {DeliveryId} failed permanently: {Error}", delivery.Id, result.Error);
                    await _store.RecordAttemptAsync(delivery.Id, attempt, DeliveryStatus.Failed, null, stoppingToken);
                    break;

                case DeliveryAction.Dead:
                    _logger.LogWarning("Delivery {DeliveryId} exhausted retries after {Attempts} attempts", delivery.Id, attemptsMade);
                    await _store.RecordAttemptAsync(delivery.Id, attempt, DeliveryStatus.Dead, null, stoppingToken);
                    break;

                case DeliveryAction.Retry:
                    var nextAttemptAt = now + decision.Delay;
                    // Record first (status Retrying) so a republish/ack failure still makes progress on redelivery.
                    await _store.RecordAttemptAsync(delivery.Id, attempt, DeliveryStatus.Retrying, nextAttemptAt, stoppingToken);
                    await RepublishToRetryAsync(delivery.Id, decision.Tier, stoppingToken);
                    break;
            }

            await SafeAckAsync(channel, ea.DeliveryTag);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await SafeNackAsync(channel, ea.DeliveryTag);
        }
        catch (Exception ex)
        {
            // Infrastructure failure (e.g. MongoDB/broker). Requeue for another attempt.
            _logger.LogError(ex, "Error handling delivery {DeliveryId}; requeueing", message.DeliveryId);
            await SafeNackAsync(channel, ea.DeliveryTag);
        }
    }

    private async Task RepublishToRetryAsync(string deliveryId, int tier, CancellationToken cancellationToken)
    {
        var channel = await _connectionManager.RentPublishChannelAsync(cancellationToken);
        try
        {
            var body = new DeliveryMessage(deliveryId).Serialize();
            var props = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = deliveryId,
            };
            await channel.BasicPublishAsync(_names.RetryExchange, _names.RetryRoutingKey(tier), mandatory: true, props, body, cancellationToken);
        }
        finally
        {
            await _connectionManager.ReturnPublishChannelAsync(channel);
        }
    }

    private async Task SafeAckAsync(IChannel channel, ulong deliveryTag)
    {
        try
        {
            await channel.BasicAckAsync(deliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ack delivery tag {DeliveryTag}", deliveryTag);
        }
    }

    private async Task SafeNackAsync(IChannel channel, ulong deliveryTag)
    {
        try
        {
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to nack delivery tag {DeliveryTag}", deliveryTag);
        }
    }

    private static DeliveryOutcomeKind ToKind(DeliveryOutcome outcome) => outcome switch
    {
        DeliveryOutcome.Delivered => DeliveryOutcomeKind.Delivered,
        DeliveryOutcome.PermanentFailure => DeliveryOutcomeKind.PermanentFailure,
        _ => DeliveryOutcomeKind.RetryableFailure,
    };

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        foreach (var channel in _channels)
        {
            await channel.DisposeAsync();
        }

        _channels.Clear();
    }
}
