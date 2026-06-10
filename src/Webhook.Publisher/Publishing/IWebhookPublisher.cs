namespace Webhook.Publisher.Publishing;

/// <summary>
/// Entry point for application code to emit an outbound webhook event. The payload
/// is stored in MongoDB and a delivery is enqueued per matching subscription.
/// </summary>
public interface IWebhookPublisher
{
    /// <summary>
    /// Records the event and enqueues a delivery for every enabled subscription of
    /// <paramref name="tenantId"/> whose filter matches <paramref name="eventName"/>.
    /// </summary>
    Task<PublishResult> PublishAsync(string tenantId, string eventName, object payload, CancellationToken cancellationToken = default);
}
