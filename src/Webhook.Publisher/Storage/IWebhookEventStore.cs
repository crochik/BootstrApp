namespace Webhook.Publisher.Storage;

/// <summary>
/// Persistence for events (with payload) and per-delivery status. This is the
/// system's source of truth; RabbitMQ only references deliveries by id.
/// </summary>
public interface IWebhookEventStore
{
    /// <summary>Creates the indexes the publisher and reconciler rely on. Idempotent.</summary>
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores a published event and its payload.</summary>
    Task SaveEventAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);

    /// <summary>Inserts the per-subscription delivery documents for an event.</summary>
    Task CreateDeliveriesAsync(IReadOnlyList<WebhookDelivery> deliveries, CancellationToken cancellationToken = default);

    Task<WebhookEvent?> GetEventAsync(string eventId, CancellationToken cancellationToken = default);

    Task<WebhookDelivery?> GetDeliveryAsync(string deliveryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims a delivery for an attempt by moving it to <c>Delivering</c>.
    /// Claimable when it is <c>Pending</c>/<c>Retrying</c>, or already <c>Delivering</c>
    /// but stale (last updated at or before <paramref name="staleDeliveringBefore"/>, i.e.
    /// a previous worker crashed mid-attempt). Returns the claimed document, or
    /// <c>null</c> if another worker holds a fresh claim (dedupe guard).
    /// </summary>
    Task<WebhookDelivery?> TryMarkDeliveringAsync(string deliveryId, DateTime now, DateTime staleDeliveringBefore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends an attempt, increments the attempt count, sets the new status and
    /// (for retries) the next scheduled time.
    /// </summary>
    Task RecordAttemptAsync(string deliveryId, DeliveryAttempt attempt, DeliveryStatus newStatus, DateTime? nextAttemptAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns deliveries the reconciler should re-enqueue: <c>Pending</c>/<c>Retrying</c>
    /// whose <c>NextAttemptAt</c> is at or before <paramref name="dueBefore"/>, or
    /// <c>Delivering</c> ones stuck since at or before <paramref name="staleDeliveringBefore"/>.
    /// </summary>
    Task<IReadOnlyList<WebhookDelivery>> GetDueDeliveriesAsync(DateTime dueBefore, DateTime staleDeliveringBefore, int limit, CancellationToken cancellationToken = default);
}
