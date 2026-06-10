namespace Webhook.Integrations.Core.Configuration;

/// <summary>
/// Configuration shared by every integration, bound from the integration's own
/// configuration section (e.g. <c>Zapier</c> or <c>N8n</c>).
/// </summary>
public sealed class IntegrationOptions
{
    /// <summary>
    /// Accepted API keys. The integration sends one as <c>X-Api-Key</c> (or
    /// <c>Authorization: Bearer</c>) on every request. An empty list disables auth —
    /// useful only for local experiments.
    /// </summary>
    public List<string> ApiKeys { get; set; } = new();

    /// <summary>Friendly name returned by the connection-test endpoint to label the connection.</summary>
    public string ConnectionName { get; set; } = "Webhook integration (mock)";

    /// <summary>
    /// Tenant the integration publishes under in <c>Webhook.Publisher</c>. All of this
    /// integration's subscriptions live under this tenant; events fan out by name
    /// (<c>"{object}.{event}"</c>). Map this to a real account if you go multi-tenant.
    /// </summary>
    public string Tenant { get; set; } = "default";

    /// <summary>
    /// Route prefix the API-key gate protects (e.g. <c>/zapier</c>, <c>/n8n</c>). Set
    /// by the integration at registration; requests outside it (such as <c>/health</c>)
    /// pass through.
    /// </summary>
    public string RoutePrefix { get; set; } = "/";
}
