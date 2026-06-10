using Crochik.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PI.Shared.App;

namespace PI.Shared.Integrations.Delivery;

/// <summary>
/// Safety net for the outbox: periodically re-enqueues deliveries that should be in
/// flight but are not — the publisher crashed between the Mongo write and the enqueue,
/// a worker died mid-attempt, or (the normal case) a retry's <c>NextAttemptAt</c> has
/// come due. The worker's atomic claim makes a redundant re-enqueue a harmless no-op.
/// This is what drives retry timing now that delays live in Mongo rather than in broker
/// delay queues.
/// </summary>
public sealed class WebhookOutboxReconciler : ILifetimeService
{
    private readonly IWebhookStore _store;
    private readonly IMessageBroker _broker;
    private readonly DeliveryOptions _options;
    private readonly ILogger<WebhookOutboxReconciler> _logger;

    private CancellationTokenSource _cts;
    private Task _loop;

    public WebhookOutboxReconciler(IWebhookStore store, IMessageBroker broker, IOptions<DeliveryOptions> options, ILogger<WebhookOutboxReconciler> logger)
    {
        _store = store;
        _broker = broker;
        _options = options.Value;
        _logger = logger;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_options.ReconcilerInterval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await SweepAsync();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox reconciler sweep failed");
            }
        }
    }

    private async Task SweepAsync()
    {
        var now = DateTime.UtcNow;
        var dueBefore = now - _options.ReconcilerGracePeriod;
        var staleDeliveringBefore = now - _options.ClaimTimeout;

        var due = await _store.GetDueDeliveriesAsync(dueBefore, staleDeliveringBefore, _options.ReconcilerBatchSize);
        if (due.Count == 0) return;

        foreach (var delivery in due)
        {
            await _broker.PublishAsync(_options.DeliveryTopic, new DeliveryDispatch { DeliveryId = delivery.Id });
        }

        _logger.LogInformation("Outbox reconciler re-enqueued {Count} due delivery/deliveries", due.Count);
    }
}
