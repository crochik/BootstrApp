namespace Webhook.Publisher.Subscriptions;

/// <summary>
/// Options bound from the <c>WebhookSubscriptions</c> configuration section,
/// backing <see cref="JsonFileWebhookSubscriptionStore"/>. Mirrors the inbound
/// service's <c>WebhookOptions</c> so subscriptions hot-reload when the JSON changes.
/// </summary>
public sealed class WebhookSubscriptionOptions
{
    public const string SectionName = "WebhookSubscriptions";

    public List<WebhookSubscription> Subscriptions { get; set; } = new();
}
