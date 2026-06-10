using Crochik.Mongo;
using PI.Shared.Integrations.Subscriptions;

namespace Zapier.Models;

/// <summary>
/// Zapier's REST Hook subscriptions. Persists into the existing
/// <c>zapier.Subscription</c> collection; all behavior comes from the shared
/// <see cref="IntegrationSubscription"/> base.
/// </summary>
[BsonCollection("zapier.Subscription")]
public class ZapierSubscription : IntegrationSubscription
{
}
