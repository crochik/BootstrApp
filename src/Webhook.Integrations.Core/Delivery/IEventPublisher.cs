namespace Webhook.Integrations.Core.Delivery;

/// <summary>
/// Emits an event to every subscription interested in it. The application calls this
/// from its domain code (or the mock emitter) whenever something happens worth
/// triggering a flow. Delivery is durable and asynchronous: the event is handed to
/// the <c>Webhook.Publisher</c> pipeline, which stores it and delivers a signed POST
/// to each subscriber with retries.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event for <paramref name="objectKey"/>/<paramref name="eventKey"/>.
    /// </summary>
    /// <returns>The number of per-subscription deliveries enqueued for delivery.</returns>
    Task<int> PublishAsync(string objectKey, string eventKey, object payload, CancellationToken ct = default);
}
