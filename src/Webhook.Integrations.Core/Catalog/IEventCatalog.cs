using System.Diagnostics.CodeAnalysis;

namespace Webhook.Integrations.Core.Catalog;

/// <summary>
/// Read-only view of the objects and events exposed to an integration.
/// Implementations <em>discover</em> the catalog (reflection, a database, a remote
/// schema service…) rather than hardcoding it, so the same controllers and
/// integration definitions serve any domain. Zapier/n8n read this through dynamic
/// dropdowns at the moment a user configures a trigger — i.e. discovery happens at
/// registration time.
/// </summary>
public interface IEventCatalog
{
    /// <summary>Every object, ordered for stable presentation in a dropdown.</summary>
    IReadOnlyList<TriggerObjectDescriptor> GetObjects();

    bool TryGetObject(string objectKey, [NotNullWhen(true)] out TriggerObjectDescriptor? descriptor);

    bool TryGetEvent(string objectKey, string eventKey, [NotNullWhen(true)] out TriggerEventDescriptor? descriptor);
}
