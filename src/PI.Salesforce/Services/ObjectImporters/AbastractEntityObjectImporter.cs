using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public abstract class AbastractEntityObjectImporter<T> : AbstractObjectImporter<T>
    where T : Entity, new()
{
    public abstract bool IsMainIdentity { get; }
    public abstract string IdentityTag { get; }

    protected bool MergeData { get; } = false;

    protected AbastractEntityObjectImporter(ILogger<AbastractEntityObjectImporter<T>> logger, MongoConnection connection, ObjectTypeService objectTypeService) :
        base(logger, connection, objectTypeService)
    {
    }

    protected override Task<T> GetAsync(IEntityContext context, SalesforceCustomObject row)
    {
        return _connection.Filter<Entity, T>(CollectionName)
            .Eq(x => x.AccountId, context.AccountId.Value)
            .ElemMatchBuilder(
                x => x.Identities,
                q => q
                    .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Salesforce))
                    .Eq(x => x.ExternalId, row.ExternalId)
            )
            .FirstOrDefaultAsync();
    }

    protected override ValueTask<WriteModel<T>> UpdateAsync(IEntityContext context, SalesforceCustomObject src, T dst)
    {
        var name = GetRequired<string>(src, "Name");
        var isActive = GetRequired<bool>(src, "IsActive");
        if (!src.TryGetProperty<bool>("IsDeleted", out var isDeleted)) isDeleted = false;
        if (isDeleted) isActive = false;

        var identity = dst.FindIdentity(nameof(ExternalProvider.Salesforce), src.ExternalId);
        if (identity == null)
        {
            return AddIdentityAsync(context, src, dst, name);
        }

        // update existing identity
        var query = _connection.Filter<T>()
            .Eq(x => x.Id, dst.Id).ElemMatchBuilder(
            x => x.Identities,
            q => q
                .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Salesforce))
                .Eq(x => x.ExternalId, src.ExternalId)
            )
            .Update;

        if (IsMainIdentity)
        {
            // main identity: set data
            if (dst.IsActive != isActive)
            {
                query.Set(x => x.IsActive, isActive);
            }

            if (MergeData)
            {
                // merge
                var changed = false;
                identity.Data ??= new Dictionary<string, object>();
                foreach (var kvp in src.Properties)
                {
                    if (identity.Data.TryGetValue(kvp.Key, out var current))
                    {
                        if (current == kvp.Value) continue;
                    }

                    _logger.LogTrace("{field} changed from {from} to {to}", kvp.Key, current, kvp.Value);
                    identity.Data[kvp.Key] = kvp.Value;
                    changed = true;
                }

                if (changed)
                {
                    query.Set($"{nameof(Entity.Identities)}.$.{nameof(EntityIdentity.Data)}", identity.Data);
                }
            }
            else
            {
                // replace
                query.Set($"{nameof(Entity.Identities)}.$.{nameof(EntityIdentity.Data)}", src.Properties);
            }

            // update  name
            query.Set(x => x.Name, name);
        }

        query
            .Set($"{nameof(Entity.Identities)}.$.{nameof(EntityIdentity.Name)}", IdentityTag ?? name)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            ;

        return ValueTask.FromResult<WriteModel<T>>(query.UpdateOneModel());
    }

    private ValueTask<WriteModel<T>> AddIdentityAsync(IEntityContext context, SalesforceCustomObject src, T dst, string name)
    {
        var query = _connection.Filter<T>()
            .Eq(x => x.Id, dst.Id)
            .Update;

        // new identity for existing entity
        var identity = new EntityIdentity
        {
            Id = Guid.NewGuid(),
            IdentityProviderId = nameof(ExternalProvider.Salesforce),
            ExternalId = src.ExternalId,
            Name = IdentityTag ?? name,
            ExternalIdentity = null,
            Data = IsMainIdentity ? src.Properties : null,
        };

        query
            .AddToSet(x => x.Identities, identity)
            .Set(x => x.LastActor, context.Actor())
        ;

        return ValueTask.FromResult<WriteModel<T>>(query.UpdateOneModel());
    }

    protected override ValueTask<WriteModel<T>> AddAsync(IEntityContext context, SalesforceCustomObject src)
    {
        var name = GetRequired<string>(src, "Name");
        var isActive = GetRequired<bool>(src, "IsActive");
        if (!src.TryGetProperty<bool>("IsDeleted", out var isDeleted)) isDeleted = false;
        if (isDeleted) isActive = false;

        // new 
        var entityId = Guid.NewGuid();
        var identityId = Guid.NewGuid();

        var entity = new T
        {
            Id = entityId,
            AccountId = context.AccountId.Value,
            EntityId = context.AccountId.Value,
            CreatedOn = DateTime.UtcNow,
            // LastActor = 
            Name = name?.ToString(),
            IsActive = isActive,
            Identities = new[]
            {
                new EntityIdentity
                {
                    Id = identityId,
                    IdentityProviderId = nameof(ExternalProvider.Salesforce),
                    ExternalId = src.ExternalId,
                    Name = name,
                    ExternalIdentity = null,
                    Data = src.Properties,
                }
            },
            ObjectStatusId = ObjectType.InitialObjectStatusId,
            FlowId = ObjectType.InitialFlowId,
            LastActor = context.Actor(),
        };

        switch (entity)
        {
            case User user:
                user.UserRoleId = EntityRoleId.User.ToString();
                user.MainIdentityId = identityId;
                break;
        }

        return ValueTask.FromResult<WriteModel<T>>(new InsertOneModel<T>(entity));
    }
}
