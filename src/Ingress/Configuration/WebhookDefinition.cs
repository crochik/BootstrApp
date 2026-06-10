using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace Ingress.Configuration;

/// <summary>
/// The full, configuration-driven definition of a single webhook endpoint.
/// Each definition is addressed by its <see cref="Uuid"/> on the route
/// <c>/ingress/{uuid}</c>. Everything about how the request is authenticated,
/// parsed, handled and answered is expressed here so that onboarding a new
/// third party requires no code changes.
///
/// Persisted (globally, keyed by <see cref="Uuid"/>) in the <c>ingress.Definition</c>
/// MongoDB collection — the inbound URL carries no tenant context, so definitions are
/// not account-scoped.
/// </summary>
[BsonCollection("ingress.Definition")]
public sealed class WebhookDefinition
{
    /// <summary>Stable identifier used as the route segment: <c>/ingress/{uuid}</c>.</summary>
    [BsonId]
    public string Uuid { get; set; } = string.Empty;

    /// <summary>Human friendly name, used in logs and handler dispatch.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When false the endpoint responds 404 as if it did not exist.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Name of the <c>IWebhookHandler</c> to dispatch to. Defaults to "logging".</summary>
    public string Handler { get; set; } = "logging";

    /// <summary>Body format: <c>json</c>, <c>form</c>, <c>xml</c> or <c>raw</c>.</summary>
    public string Format { get; set; } = "json";

    /// <summary>
    /// Ordered list of validators that must ALL pass (logical AND). An empty
    /// list, or a single entry of type <c>none</c>, means no authentication.
    /// </summary>
    public List<AuthConfig> Auth { get; set; } = new();

    /// <summary>Registration / verification handshake behaviour.</summary>
    public RegistrationConfig Registration { get; set; } = new();

    /// <summary>Response returned on a successful (non-handshake) delivery.</summary>
    public ResponseConfig Response { get; set; } = new();
}
