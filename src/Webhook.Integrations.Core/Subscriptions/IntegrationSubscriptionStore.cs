using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Webhook.Integrations.Core.Configuration;
using PublisherSubscription = Webhook.Publisher.Subscriptions.WebhookSubscription;
using IPublisherSubscriptionStore = Webhook.Publisher.Subscriptions.IWebhookSubscriptionStore;

namespace Webhook.Integrations.Core.Subscriptions;

/// <summary>
/// Bridges an integration's REST Hook lifecycle to the <c>Webhook.Publisher</c>
/// delivery pipeline. A single instance is exposed as two interfaces:
/// <list type="bullet">
/// <item><see cref="ISubscriptionStore"/> — used by the controllers to add and remove
/// subscriptions as triggers are activated/deactivated.</item>
/// <item><c>Webhook.Publisher.Subscriptions.IWebhookSubscriptionStore</c> — read by the
/// publisher to resolve which endpoints receive a given event.</item>
/// </list>
/// A subscription on <c>(object, event)</c> maps to a publisher subscription under a
/// single tenant with event name <c>"{object}.{event}"</c>.
/// </summary>
public sealed class IntegrationSubscriptionStore : ISubscriptionStore, IPublisherSubscriptionStore
{
    // Keyed by subscription id; carries both the integration view and the publisher view.
    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private readonly IOptions<IntegrationOptions> _options;
    private long _sequence;

    public IntegrationSubscriptionStore(IOptions<IntegrationOptions> options)
    {
        _options = options;
    }

    private string Tenant => _options.Value.Tenant;

    // ---- Integration-facing API (ISubscriptionStore) ---------------------------

    public Subscription Add(string objectKey, string eventKey, string targetUrl)
    {
        var id = $"sub_{Interlocked.Increment(ref _sequence):D6}_{Guid.NewGuid():N}";
        var createdAt = DateTimeOffset.UtcNow;

        var publisherSubscription = new PublisherSubscription
        {
            Id = id,
            TenantId = Tenant,
            Url = targetUrl,
            // Generated per-subscription so the signed delivery carries a real secret;
            // the integration may ignore it unless it opts into verifying signatures.
            Secret = "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)),
            Events = new List<string> { EventName(objectKey, eventKey) },
            SignatureHeader = "Webhook-Signature",
            Enabled = true,
        };

        var subscription = new Subscription(id, objectKey, eventKey, targetUrl, createdAt);
        _entries[id] = new Entry(subscription, publisherSubscription);
        return subscription;
    }

    public bool Remove(string id) => _entries.TryRemove(id, out _);

    public IReadOnlyList<Subscription> Find(string objectKey, string eventKey) =>
        _entries.Values
            .Where(e => string.Equals(e.Integration.ObjectKey, objectKey, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(e.Integration.EventKey, eventKey, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Integration)
            .OrderBy(s => s.CreatedAt)
            .ToList();

    public IReadOnlyList<Subscription> All() =>
        _entries.Values.Select(e => e.Integration).OrderBy(s => s.CreatedAt).ToList();

    // ---- Publisher-facing API (IWebhookSubscriptionStore) ----------------------

    public Task<IReadOnlyList<PublisherSubscription>> GetForAsync(string tenantId, string eventName, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PublisherSubscription> matches = _entries.Values
            .Select(e => e.Publisher)
            .Where(s => s.Enabled
                        && string.Equals(s.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
                        && s.Matches(eventName))
            .ToList();
        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<PublisherSubscription>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PublisherSubscription> all = _entries.Values.Select(e => e.Publisher).ToList();
        return Task.FromResult(all);
    }

    internal static string EventName(string objectKey, string eventKey) => $"{objectKey}.{eventKey}";

    private sealed record Entry(Subscription Integration, PublisherSubscription Publisher);
}
