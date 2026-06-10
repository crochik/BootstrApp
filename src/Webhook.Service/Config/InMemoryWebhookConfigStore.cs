using Webhook.Service.Configuration;

namespace Webhook.Service.Config;

/// <summary>
/// In-memory store, primarily for tests and local experimentation.
/// </summary>
public sealed class InMemoryWebhookConfigStore : IWebhookConfigStore
{
    private readonly Dictionary<string, WebhookDefinition> _byUuid;

    public InMemoryWebhookConfigStore(IEnumerable<WebhookDefinition> definitions)
    {
        _byUuid = definitions.ToDictionary(d => d.Uuid, StringComparer.OrdinalIgnoreCase);
    }

    public Task<WebhookDefinition?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
    {
        _byUuid.TryGetValue(uuid, out var definition);
        if (definition is { Enabled: false })
        {
            definition = null;
        }

        return Task.FromResult(definition);
    }

    public Task<IReadOnlyList<WebhookDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WebhookDefinition> all = _byUuid.Values.ToList();
        return Task.FromResult(all);
    }
}
