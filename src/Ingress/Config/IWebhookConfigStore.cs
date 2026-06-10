using Ingress.Configuration;

namespace Ingress.Config;

/// <summary>
/// Source of webhook definitions. The controller and processor depend only on
/// this abstraction, so swapping the JSON-file backing store for a database (or
/// any other source) later requires no changes to the request pipeline.
/// </summary>
public interface IWebhookConfigStore
{
    /// <summary>Returns the definition for the given UUID, or null if none/disabled.</summary>
    Task<WebhookDefinition?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default);

    /// <summary>Returns all currently known definitions.</summary>
    Task<IReadOnlyList<WebhookDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
}
