using PI.Shared.Models;

namespace PI.Shared.Integrations.Catalog;

/// <summary>
/// Read-only view of the objects and events exposed to an integration for a given
/// caller. Unlike a hardcoded list, the catalog is <em>discovered</em> from the
/// account's real <c>ObjectType</c> definitions, so the same controllers and
/// integration apps serve any domain. Zapier/n8n read this through dynamic dropdowns
/// at the moment a user configures a trigger — discovery happens at registration time.
/// </summary>
public interface IObjectCatalog
{
    /// <summary>Every triggerable object for the context, ordered for stable presentation.</summary>
    Task<IReadOnlyList<TriggerObjectDescriptor>> GetObjectsAsync(IEntityContext context);

    /// <summary>The descriptor for one object key, or <c>null</c> if it is not exposed.</summary>
    Task<TriggerObjectDescriptor?> GetObjectAsync(IEntityContext context, string objectKey);

    /// <summary>Resolves a single event on an object, or <c>null</c> if either is unknown.</summary>
    Task<TriggerEventDescriptor?> GetEventAsync(IEntityContext context, string objectKey, string eventKey);
    
    /// <summary>
    /// Get events for object
    /// </summary>
    Task<IReadOnlyList<TriggerEventDescriptor>> GetEventsAsync(IEntityContext context, string objectKey);
}
