using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.Shared.Integrations.Catalog;

/// <summary>
/// <see cref="IObjectCatalog"/> backed by the account's real <c>ObjectType</c>
/// definitions. Objects are the (non-embedded, non-abstract) object types the caller
/// can read; events are the platform lifecycle — Create/Update/Delete — which is the
/// same vocabulary <c>FlowObjectEventRoute</c> uses on the wire.
/// </summary>
public sealed class ObjectTypeCatalog : IObjectCatalog
{
    /// <summary>
    /// Lifecycle every object exposes. Keys match <see cref="FlowObjectEventRoute"/>
    /// names so a subscription's stored key equals the action carried on the
    /// <c>object.{type}.{id}.{action}</c> routing key the listener receives.
    /// </summary>
    public static readonly IReadOnlyList<TriggerEventDescriptor> Lifecycle = new[]
    {
        new TriggerEventDescriptor(nameof(FlowObjectEventRoute.Create), "Created", "Fires when the object is created."),
        new TriggerEventDescriptor(nameof(FlowObjectEventRoute.Update), "Updated", "Fires when the object is updated."),
        new TriggerEventDescriptor(nameof(FlowObjectEventRoute.Delete), "Deleted", "Fires when the object is deleted."),
    };

    private readonly MongoConnection _connection;

    public ObjectTypeCatalog(MongoConnection connection)
    {
        _connection = connection;
    }

    public async Task<IReadOnlyList<TriggerObjectDescriptor>> GetObjectsAsync(IEntityContext context)
    {
        var objectTypes = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, context.AccountId)
            .Ne(x => x.IsEmbedded, true)
            .Ne(x => x.IsAbstract, true)
            .FindAsync();

        return objectTypes
            .Where(ot => !string.IsNullOrWhiteSpace(ot.Name))
            .Select(Describe)
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<TriggerObjectDescriptor?> GetObjectAsync(IEntityContext context, string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey)) return null;

        var objectType = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Name, objectKey)
            .FirstOrDefaultAsync();

        return objectType is null ? null : Describe(objectType);
    }

    public async Task<TriggerEventDescriptor?> GetEventAsync(IEntityContext context, string objectKey, string eventKey)
    {
        var obj = await GetObjectAsync(context, objectKey);
        return obj?.Events.FirstOrDefault(e => string.Equals(e.Key, eventKey, StringComparison.OrdinalIgnoreCase));
    }

    private static TriggerObjectDescriptor Describe(ObjectType ot)
    {
        var label = !string.IsNullOrWhiteSpace(ot.Label) ? ot.Label : ot.Name;
        var noun = !string.IsNullOrWhiteSpace(ot.LabelPlural) ? ot.LabelPlural : label;
        var description = !string.IsNullOrWhiteSpace(ot.Description) ? ot.Description : $"A {label}.";
        return new TriggerObjectDescriptor(ot.Name, label, noun, description, Lifecycle);
    }
}
