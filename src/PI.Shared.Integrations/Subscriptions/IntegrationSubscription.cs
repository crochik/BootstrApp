using PI.Shared.Models;

namespace PI.Shared.Integrations.Subscriptions;

/// <summary>
/// A REST Hook subscription: an integration's request to be POSTed a payload whenever
/// a specific event fires on a specific object. Created when the integration registers
/// a callback URL (a Zap turned on, an n8n workflow activated); removed on teardown.
/// <para>
/// Abstract so each integration persists into its own collection — a concrete subclass
/// just adds <c>[BsonCollection("&lt;integration&gt;.Subscription")]</c>. Carries the full
/// account/profile context so the per-subscriber payload is built with the right RBAC.
/// </para>
/// </summary>
public abstract class IntegrationSubscription : EntityOwnedModel
{
    /// <summary>Object type subscribed to (the <c>ObjectType.Name</c>).</summary>
    public string ObjectType { get; set; }

    /// <summary>Organization scope of the subscription, when narrowed to one.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Callback URL that receives the signed POST.</summary>
    public string Url { get; set; }

    /// <summary>Subscribed lifecycle keys (<c>Create</c>/<c>Update</c>/<c>Delete</c>).</summary>
    public string[] Keys { get; set; }

    /// <summary>Profile whose RBAC is used to flatten the delivered object.</summary>
    public Guid ProfileId { get; set; }

    public string ClientId { get; set; }

    /// <summary>Per-subscription HMAC signing secret (<c>whsec_…</c>).</summary>
    public string Secret { get; set; }

    /// <summary>Header that carries the signature on each delivery.</summary>
    public string SignatureHeader { get; set; } = "Webhook-Signature";
}
