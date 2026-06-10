namespace Webhook.Integrations.Core.Subscriptions;

/// <summary>
/// A REST Hook subscription: the integration's request to be POSTed a payload
/// whenever a specific event fires on a specific object. Created when the integration
/// registers a target URL (a Zap turned on, an n8n workflow activated); removed when
/// it is torn down.
/// </summary>
public sealed record Subscription(
    string Id,
    string ObjectKey,
    string EventKey,
    string TargetUrl,
    DateTimeOffset CreatedAt);
