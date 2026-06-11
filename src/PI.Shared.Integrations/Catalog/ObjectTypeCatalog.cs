using System.Collections.Immutable;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.Shared.Integrations.Catalog;

/// <summary>
/// <see cref="IObjectCatalog"/> backed by the account's real <c>ObjectType</c>
/// definitions. Objects are the (non-embedded, non-abstract) object types the caller
/// can read; events are the platform lifecycle — Create/Update/Delete — which is the
/// same vocabulary <c>FlowObjectEventRoute</c> uses on the wire.
/// </summary>
public sealed class ObjectTypeCatalog(MongoConnection connection, ObjectTypeService objectTypeService) : IObjectCatalog
{
    public async Task<IReadOnlyList<TriggerObjectDescriptor>> GetObjectsAsync(IEntityContext context)
    {
        var objectTypes = await connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, context.AccountId)
            .BitsAllSet(x => x.RBAC.Permissions[context.ProfileId.ToString()], 1)
            .Ne(x => x.IsEmbedded, true)
            .Ne(x => x.IsAbstract, true)
            .FindAsync();

        return objectTypes
            .Select(Describe)
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<TriggerObjectDescriptor?> GetObjectAsync(IEntityContext context, string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey)) return null;

        var objectType = await connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Name, objectKey)
            .FirstOrDefaultAsync();

        return objectType is null ? null : Describe(objectType);
    }

    public async Task<TriggerEventDescriptor?> GetEventAsync(IEntityContext context, string objectKey, string eventKey)
    {
        throw new NotImplementedException();
        // var obj = await GetObjectAsync(context, objectKey);
        // return obj?.Events.FirstOrDefault(e => string.Equals(e.Key, eventKey, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<TriggerEventDescriptor>> GetEventsAsync(IEntityContext context, string objectKey)
    {
        var objectType = await objectTypeService.GetAsync(context, objectKey);
        if (objectType is null)
        {
            // error
            return [];
        }

        if (objectType.UsesDefaultFlow)
        {
            // TODO: load flow and get actions from it
            // objectType.InitialFlowId
            // ...
            return [];
        }

        if (!objectType.TryGetObjectTypeFromFlowField(out var objectTypeName))
        {
            objectTypeName = objectKey;
        }

        var flows = await connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.ObjectType, objectTypeName)
            .ElemMatchBuilder(f => f.Steps, q => q.Eq(x => x.ActionId, ActionIds.FireWebhook))
            .FindAsync();

        var steps = flows
            .SelectMany(x => x.Steps
                .Where(q => q.ActionId == ActionIds.FireWebhook)
            )
            .ToArray();

        return steps
            .Select(x => (x.Options as GenericActionOptions)?.ConvertTo<FireWebhookActionOptions>())
            .Where(x => x != null)
            .DistinctBy(x => x!.EventId)
            .Select(x => new TriggerEventDescriptor(x!.EventId, x.EventDescription, x.EventDescription))
            .ToImmutableList();
    }

    private static TriggerObjectDescriptor Describe(ObjectType ot)
    {
        var label = ot.LabelPlural ?? ot.Label ?? ot.Name;
        var description = !string.IsNullOrWhiteSpace(ot.Description) ? ot.Description : $"A {label}.";
        return new TriggerObjectDescriptor(ot.FullName, label, description);
    }
}

public class FireWebhookActionOptions : ActionOptions
{
    public string EventId { get; set; }
    public string EventName { get; set; }
    public string EventDescription { get; set; }
}