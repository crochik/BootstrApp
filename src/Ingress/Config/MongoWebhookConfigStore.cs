using Crochik.Mongo;
using Ingress.Configuration;

namespace Ingress.Config;

/// <summary>
/// MongoDB-backed <see cref="IWebhookConfigStore"/>. Definitions live in the
/// <c>ingress.Definition</c> collection (see <see cref="WebhookDefinition"/>) and are
/// looked up globally by their <c>Uuid</c> route segment.
/// </summary>
public sealed class MongoWebhookConfigStore : IWebhookConfigStore
{
    private readonly MongoConnection _connection;

    public MongoWebhookConfigStore(MongoConnection connection)
    {
        _connection = connection;
    }

    public async Task<WebhookDefinition?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
    {
        var definition = await _connection.Filter<WebhookDefinition>()
            .Eq(x => x.Uuid, uuid)
            .FirstOrDefaultAsync();

        // A disabled definition responds 404 as if it did not exist.
        return definition is { Enabled: true } ? definition : null;
    }

    public async Task<IReadOnlyList<WebhookDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _connection.Filter<WebhookDefinition>().FindAsync();
    }
}
