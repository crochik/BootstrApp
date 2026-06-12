using PI.Shared.Models;

namespace PI.Shared.Integrations.Subscriptions;

/// <summary>
/// Stores an integration's REST Hook subscriptions, scoped to the calling context.
/// The shipped <see cref="MongoSubscriptionStore{T}"/> persists each integration into
/// its own collection; the controllers and the event listener depend only on this
/// interface.
/// </summary>
public interface ISubscriptionStore
{
    /// <summary>
    /// Registers a callback for <paramref name="objectKey"/>/<paramref name="eventKey"/>.
    /// Any existing subscription from the same caller to the same URL is replaced.
    /// </summary>
    Task<IntegrationSubscription> AddAsync(IContextWithActor context, string objectKey, string eventKey, string targetUrl);

    /// <summary>Removes a subscription by id within the caller's account. Returns <c>false</c> if it was already gone.</summary>
    Task<bool> RemoveAsync(IEntityContext context, Guid id);

    /// <summary>All of the caller's subscriptions, in creation order.</summary>
    Task<IReadOnlyList<IntegrationSubscription>> ListAsync(IEntityContext context);

    /// <summary>A single subscription owned by the caller, or <c>null</c>.</summary>
    Task<IntegrationSubscription> GetAsync(IEntityContext context, Guid id);

    /// <summary>The caller's subscriptions for a given object/event, in creation order.</summary>
    Task<IReadOnlyList<IntegrationSubscription>> FindAsync(IEntityContext context, string objectKey, string eventKey);

    /// <summary>
    /// Resolves every subscription that should receive an object event — used by the
    /// delivery listener, which has only the event's account and target, not a request
    /// context. Matches the account, object type and the subscribed key
    /// </summary>
    Task<IReadOnlyList<IntegrationSubscription>> FindForDeliveryAsync(Guid accountId, Guid? organizationId, string objectKey, string eventKey);
}
