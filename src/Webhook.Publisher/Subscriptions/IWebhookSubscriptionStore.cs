namespace Webhook.Publisher.Subscriptions;

/// <summary>
/// Resolves the set of endpoints that should receive a given tenant's event.
/// Pluggable, mirroring the inbound service's <c>IWebhookConfigStore</c>; swap the
/// JSON-file implementation for a database-backed one without touching the publisher.
/// </summary>
public interface IWebhookSubscriptionStore
{
    /// <summary>Returns every enabled subscription for the tenant whose filter matches the event.</summary>
    Task<IReadOnlyList<WebhookSubscription>> GetForAsync(string tenantId, string eventName, CancellationToken cancellationToken = default);

    /// <summary>Returns all subscriptions (enabled and disabled).</summary>
    Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken = default);
}
