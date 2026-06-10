namespace Webhook.Publisher.Publishing;

/// <summary>
/// Outcome of publishing an event: the stored event id, how many per-subscription
/// deliveries were enqueued, and whether the broker confirmed all of them.
/// </summary>
public readonly record struct PublishResult(string EventId, int DeliveriesEnqueued, bool AllAcknowledged);
