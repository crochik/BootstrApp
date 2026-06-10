namespace Webhook.Publisher.Subscriptions;

/// <summary>
/// In-memory subscription store, primarily for tests and simple hosting scenarios.
/// </summary>
public sealed class InMemoryWebhookSubscriptionStore : IWebhookSubscriptionStore
{
    private readonly IReadOnlyList<WebhookSubscription> _subscriptions;

    public InMemoryWebhookSubscriptionStore(IEnumerable<WebhookSubscription> subscriptions)
    {
        _subscriptions = subscriptions.ToList();
    }

    public Task<IReadOnlyList<WebhookSubscription>> GetForAsync(string tenantId, string eventName, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WebhookSubscription> matches = _subscriptions
            .Where(s => s.Enabled
                        && string.Equals(s.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
                        && s.Matches(eventName))
            .ToList();
        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_subscriptions);
}
