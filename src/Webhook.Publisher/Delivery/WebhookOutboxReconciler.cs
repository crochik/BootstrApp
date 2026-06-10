using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Webhook.Publisher.Configuration;
using Webhook.Publisher.Messaging;
using Webhook.Publisher.Storage;

namespace Webhook.Publisher.Delivery;

/// <summary>
/// Safety net for the "outbox": periodically re-enqueues deliveries that should be
/// in flight but are not — e.g. the publisher crashed between the MongoDB write and
/// the enqueue, a delay-tier comeback was lost, or a worker died mid-attempt. The
/// worker's atomic claim makes a redundant re-enqueue a harmless no-op.
/// </summary>
public sealed class WebhookOutboxReconciler : BackgroundService
{
    private readonly IWebhookEventStore _store;
    private readonly IWebhookConnectionManager _connectionManager;
    private readonly DeliveryOptions _options;
    private readonly WebhookTopologyNames _names;
    private readonly ILogger<WebhookOutboxReconciler> _logger;

    public WebhookOutboxReconciler(
        IWebhookEventStore store,
        IWebhookConnectionManager connectionManager,
        IOptions<WebhookPublisherOptions> options,
        WebhookTopologyNames names,
        ILogger<WebhookOutboxReconciler> logger)
    {
        _store = store;
        _connectionManager = connectionManager;
        _options = options.Value.Delivery;
        _names = names;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.ReconcilerInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox reconciler sweep failed");
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var dueBefore = now - _options.ReconcilerGracePeriod;
        var staleDeliveringBefore = now - _options.ClaimTimeout;

        var due = await _store.GetDueDeliveriesAsync(dueBefore, staleDeliveringBefore, _options.ReconcilerBatchSize, cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        var channel = await _connectionManager.RentPublishChannelAsync(cancellationToken);
        try
        {
            foreach (var delivery in due)
            {
                var body = new DeliveryMessage(delivery.Id).Serialize();
                var props = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    MessageId = delivery.Id,
                };
                var routingKey = RoutingKey.For(delivery.TenantId, delivery.EventName).Value;
                await channel.BasicPublishAsync(_names.DeliveryExchange, routingKey, mandatory: true, props, body, cancellationToken);
            }
        }
        finally
        {
            await _connectionManager.ReturnPublishChannelAsync(channel);
        }

        _logger.LogInformation("Outbox reconciler re-enqueued {Count} due delivery/deliveries", due.Count);
    }
}
