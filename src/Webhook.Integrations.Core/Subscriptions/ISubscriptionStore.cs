namespace Webhook.Integrations.Core.Subscriptions;

/// <summary>
/// Stores an integration's REST Hook subscriptions. The shipped
/// <see cref="IntegrationSubscriptionStore"/> keeps them in memory and bridges to the
/// publisher; swap in a database-backed implementation without touching the controllers.
/// </summary>
public interface ISubscriptionStore
{
    Subscription Add(string objectKey, string eventKey, string targetUrl);

    /// <summary>Removes a subscription by id. Returns <c>false</c> if it was already gone.</summary>
    bool Remove(string id);

    /// <summary>All subscriptions interested in a given object/event, in creation order.</summary>
    IReadOnlyList<Subscription> Find(string objectKey, string eventKey);

    IReadOnlyList<Subscription> All();
}
