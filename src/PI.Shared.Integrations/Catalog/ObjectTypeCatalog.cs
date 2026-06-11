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

        var query = connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId)
            .ElemMatchBuilder(f => f.Steps, q => q.Eq(x => x.ActionId, ActionIds.FireWebhook));

        if (objectType.UsesDefaultFlow)
        {
            // load flow
            query.Eq(x => x.Id, objectType.InitialFlowId);
        }
        else
        {
            // load all flows for the object type
            if (!objectType.TryGetObjectTypeFromFlowField(out var objectTypeName))
            {
                objectTypeName = objectKey;
            }

            query.Eq(x => x.ObjectType, objectTypeName);
        }

        var flows = await query.FindAsync();
        var steps = flows
            .SelectMany(x => x.Steps
                .Where(q => q.ActionId == ActionIds.FireWebhook)
            )
            .ToArray();

        return steps
            .Select(x => (x.Options as GenericActionOptions)?.ConvertTo<FireWebhookActionOptions>())
            .Where(x => x != null)
            .DistinctBy(x => x!.EventId)
            .Select(x => new TriggerEventDescriptor(x!.EventId, x.EventName, x.EventDescription))
            .ToImmutableList();
    }

    private static TriggerObjectDescriptor Describe(ObjectType ot)
    {
        var label = ot.LabelPlural ?? ot.Label ?? ot.Name;
        var description = !string.IsNullOrWhiteSpace(ot.Description) ? ot.Description : $"A {label}.";
        return new TriggerObjectDescriptor(ot.FullName, label, description);
    }
}