using Microsoft.Extensions.Options;

namespace Webhook.Publisher.Subscriptions;

/// <summary>
/// Subscription store backed by <see cref="WebhookSubscriptionOptions"/>, bound from
/// a JSON configuration source with <c>reloadOnChange</c> enabled. Editing the JSON
/// updates the subscriptions without a restart. Mirrors the inbound service's
/// <c>JsonFileWebhookConfigStore</c>.
/// </summary>
public sealed class JsonFileWebhookSubscriptionStore : IWebhookSubscriptionStore
{
    private readonly IOptionsMonitor<WebhookSubscriptionOptions> _options;

    public JsonFileWebhookSubscriptionStore(IOptionsMonitor<WebhookSubscriptionOptions> options)
    {
        _options = options;
    }

    public Task<IReadOnlyList<WebhookSubscription>> GetForAsync(string tenantId, string eventName, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WebhookSubscription> matches = _options.CurrentValue.Subscriptions
            .Where(s => s.Enabled
                        && string.Equals(s.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
                        && s.Matches(eventName))
            .ToList();
        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WebhookSubscription> all = _options.CurrentValue.Subscriptions.ToList();
        return Task.FromResult(all);
    }
}
