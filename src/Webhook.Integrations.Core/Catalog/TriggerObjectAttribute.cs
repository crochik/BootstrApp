namespace Webhook.Integrations.Core.Catalog;

/// <summary>
/// Marks a POCO as a trigger-exposable object. The catalog discovers every type
/// carrying this attribute at startup, so dropping a new decorated class into the
/// app is enough for a new object (and its events) to appear in every integration
/// (Zapier, n8n, …) — with no changes to the services, controllers, or the
/// integration definitions.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TriggerObjectAttribute : Attribute
{
    /// <summary>Stable machine key (e.g. <c>contact</c>). Defaults to the type name, normalized.</summary>
    public string? Key { get; init; }

    /// <summary>Human label (e.g. <c>Contact</c>). Defaults to a humanized type name.</summary>
    public string? Label { get; init; }

    /// <summary>Singular noun used when naming triggers. Defaults to <see cref="Label"/>.</summary>
    public string? Noun { get; init; }

    /// <summary>Short description surfaced to the end user.</summary>
    public string? Description { get; init; }
}
