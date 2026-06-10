namespace Webhook.Publisher.Subscriptions;

/// <summary>
/// A tenant's outbound webhook endpoint. A delivery document is created per
/// matching subscription when an event is published, snapshotting the URL/secret
/// so later configuration changes do not retro-actively affect in-flight deliveries.
/// </summary>
public sealed class WebhookSubscription
{
    /// <summary>Stable identifier for this subscription (used to correlate deliveries).</summary>
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    /// <summary>Destination URL that receives the signed POST.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Per-subscription HMAC signing secret.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>When false the subscription is ignored.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Event filter. Empty or containing <c>"*"</c> means "all events".</summary>
    public List<string> Events { get; set; } = new();

    /// <summary>Header that carries the signature, e.g. <c>Webhook-Signature</c>.</summary>
    public string SignatureHeader { get; set; } = "Webhook-Signature";

    /// <summary>True when this subscription should receive the given event.</summary>
    public bool Matches(string eventName) =>
        Events.Count == 0
        || Events.Contains("*")
        || Events.Any(e => string.Equals(e, eventName, StringComparison.OrdinalIgnoreCase));
}
