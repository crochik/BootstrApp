using Crochik.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PI.Shared.App;

namespace PI.Shared.Integrations.Delivery;

/// <summary>
/// Consumes delivery references from the broker, delivers them over HTTP and records
/// the outcome. On a retryable failure it reschedules the delivery in MongoDB
/// (<c>Retrying</c> + <c>NextAttemptAt</c>); the <see cref="WebhookOutboxReconciler"/>
/// re-enqueues it when due. MongoDB is the source of truth for status and attempts.
/// </summary>
public sealed class WebhookDeliveryWorker : AbstractMessageQueueService, ILifetimeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebhookStore _store;
    private readonly DeliveryOptions _options;

    public WebhookDeliveryWorker(
        ILogger<WebhookDeliveryWorker> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        IServiceScopeFactory scopeFactory,
        IWebhookStore store,
        IOptions<DeliveryOptions> options)
        : base(logger, configuration, messageBroker)
    {
        _scopeFactory = scopeFactory;
        _store = store;
        _options = options.Value;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, _options.DeliveryTopic);
        mapper.Register<DeliveryDispatch>();
    }

    protected override async Task OnMessageAsync(IMessage message)
    {
        try
        {
            if (message.Body is DeliveryDispatch dispatch)
            {
                await HandleAsync(dispatch.DeliveryId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process delivery message {RoutingKey}", message.RoutingKey);
        }

        message.Acknowledge();
    }

    private async Task HandleAsync(Guid deliveryId)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now - _options.ClaimTimeout;

        var delivery = await _store.TryClaimAsync(deliveryId, now, staleBefore);
        if (delivery is null)
        {
            // Already claimed, delivered or terminal — duplicate. Idempotent no-op.
            return;
        }

        var attemptsMade = delivery.AttemptCount + 1;
        var firstAttemptAt = delivery.FirstAttemptAt ?? now;

        var webhookEvent = await _store.GetEventAsync(delivery.EventId);
        if (webhookEvent is null)
        {
            Logger.LogError("Event {EventId} for delivery {DeliveryId} not found; marking Failed", delivery.EventId, delivery.Id);
            await _store.RecordAttemptAsync(delivery.Id, new DeliveryAttempt
            {
                Number = attemptsMade,
                At = now,
                Outcome = DeliveryOutcome.PermanentFailure,
                Error = "event not found",
            }, DeliveryStatus.Failed, null);
            return;
        }

        DeliveryResult result;
        using (var scope = _scopeFactory.CreateScope())
        {
            var client = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryClient>();
            result = await client.DeliverAsync(delivery, webhookEvent);
        }

        var decision = RetrySchedule.Decide(attemptsMade, firstAttemptAt, now, result.Outcome, _options);
        var attempt = new DeliveryAttempt
        {
            Number = attemptsMade,
            At = now,
            StatusCode = result.StatusCode,
            Outcome = result.Outcome,
            Error = result.Error,
            DurationMs = result.DurationMs,
        };

        switch (decision.Action)
        {
            case DeliveryAction.Complete:
                await _store.RecordAttemptAsync(delivery.Id, attempt, DeliveryStatus.Delivered, null);
                break;

            case DeliveryAction.Fail:
                Logger.LogWarning("Delivery {DeliveryId} failed permanently: {Error}", delivery.Id, result.Error);
                await _store.RecordAttemptAsync(delivery.Id, attempt, DeliveryStatus.Failed, null);
                break;

            case DeliveryAction.Dead:
                Logger.LogWarning("Delivery {DeliveryId} exhausted retries after {Attempts} attempts", delivery.Id, attemptsMade);
                await _store.RecordAttemptAsync(delivery.Id, attempt, DeliveryStatus.Dead, null);
                break;

            case DeliveryAction.Retry:
                // Reschedule in Mongo; the reconciler re-enqueues it when NextAttemptAt is due.
                await _store.RecordAttemptAsync(delivery.Id, attempt, DeliveryStatus.Retrying, now + decision.Delay);
                break;
        }
    }
}
