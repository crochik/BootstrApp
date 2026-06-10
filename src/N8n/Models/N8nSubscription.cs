using Crochik.Mongo;
using PI.Shared.Integrations.Subscriptions;

namespace N8n.Models;

/// <summary>
/// n8n's REST Hook subscriptions. Persists into the <c>n8n.Subscription</c> collection;
/// all behavior comes from the shared <see cref="IntegrationSubscription"/> base.
/// </summary>
[BsonCollection("n8n.Subscription")]
public class N8nSubscription : IntegrationSubscription
{
}
