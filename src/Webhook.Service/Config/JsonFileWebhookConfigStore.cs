using Microsoft.Extensions.Options;
using Webhook.Service.Configuration;

namespace Webhook.Service.Config;

/// <summary>
/// Config store backed by <see cref="WebhookOptions"/>, which is bound from a
/// JSON configuration source with <c>reloadOnChange</c> enabled. Editing the
/// JSON file therefore updates the available webhooks without a restart.
/// </summary>
public sealed class JsonFileWebhookConfigStore : IWebhookConfigStore
{
    private readonly IOptionsMonitor<WebhookOptions> _options;

    public JsonFileWebhookConfigStore(IOptionsMonitor<WebhookOptions> options)
    {
        _options = options;
    }

    public Task<WebhookDefinition?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
    {
        var match = _options.CurrentValue.Definitions
            .FirstOrDefault(d => d.Enabled &&
                                 string.Equals(d.Uuid, uuid, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task<IReadOnlyList<WebhookDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WebhookDefinition> all = _options.CurrentValue.Definitions.ToList();
        return Task.FromResult(all);
    }
}
