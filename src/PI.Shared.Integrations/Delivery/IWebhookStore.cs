namespace PI.Shared.Integrations.Delivery;

/// <summary>
/// Persistence for events (with payload) and per-delivery status. This is the system's
/// source of truth; the broker only references deliveries by id.
/// </summary>
public interface IWebhookStore
{
    /// <summary>Stores a published event and its payload.</summary>
    Task SaveEventAsync(WebhookEvent webhookEvent);

    /// <summary>Inserts the per-subscription delivery documents for an event.</summary>
    Task CreateDeliveriesAsync(IReadOnlyList<WebhookDelivery> deliveries);

    Task<WebhookEvent> GetEventAsync(Guid eventId);

    /// <summary>
    /// Atomically claims a delivery for an attempt by moving it to <c>Delivering</c>.
    /// Claimable when <c>Pending</c>/<c>Retrying</c>, or already <c>Delivering</c> but
    /// stale (updated at/before <paramref name="staleDeliveringBefore"/> — a previous
    /// worker crashed mid-attempt). Returns the claimed document, or <c>null</c> if
    /// another worker holds a fresh claim (dedupe guard).
    /// </summary>
    Task<WebhookDelivery> TryClaimAsync(Guid deliveryId, DateTime now, DateTime staleDeliveringBefore);

    /// <summary>
    /// Appends an attempt, increments the attempt count, sets the new status and (for
    /// retries) the next scheduled time.
    /// </summary>
    Task RecordAttemptAsync(Guid deliveryId, DeliveryAttempt attempt, DeliveryStatus newStatus, DateTime? nextAttemptAt);

    /// <summary>
    /// Returns deliveries the reconciler should re-enqueue: <c>Pending</c>/<c>Retrying</c>
    /// whose <c>NextAttemptAt</c> is at/before <paramref name="dueBefore"/>, or
    /// <c>Delivering</c> ones stuck since at/before <paramref name="staleDeliveringBefore"/>.
    /// </summary>
    Task<IReadOnlyList<WebhookDelivery>> GetDueDeliveriesAsync(DateTime dueBefore, DateTime staleDeliveringBefore, int limit);
}
