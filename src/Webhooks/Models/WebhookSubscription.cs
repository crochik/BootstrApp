using Crochik.Mongo;
using PI.Shared.Integrations.Subscriptions;

namespace Webhooks.Models;

/// <summary>
/// Generic webhook subscriptions for any application. Persists into the
/// <c>webhooks.Subscription</c> collection; all behavior comes from the shared
/// <see cref="IntegrationSubscription"/> base.
/// </summary>
[BsonCollection("webhooks.Subscription")]
public class WebhookSubscription : IntegrationSubscription
{
}
