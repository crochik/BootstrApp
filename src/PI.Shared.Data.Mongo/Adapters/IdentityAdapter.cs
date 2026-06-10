using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class EntityIdentityAdapter : IEntityIdentityAdapter
    {
        private MongoConnection _connection;

        public EntityIdentityAdapter(MongoConnection connection)
        {
            _connection = connection;
        }

        public async Task<IEntity> GetEntityByIdAsync(Guid entityId)
            => await _connection.GetByIdAsync<Entity>(entityId);

        public async Task<EntityIdentity> AddAsync(IEntityContext context, Guid entityId, EntityIdentity identity)
        {
            var dao = _connection.Map<EntityIdentity>(identity);
            var entity = await _connection
                .Filter<Entity>().Eq(x => x.Id, entityId)
                .Update
                    .AddToSet(x => x.Identities, identity)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, context.Actor())
                    .UpdateAndGetOneAsync();

            var result = entity?.Identities?.FirstOrDefault(x => x.Id == identity.Id);

            return result;
        }

        public async Task<bool> UpdateTokenAsync(IEntity entity, EntityIdentity externalIdentity)
        {
            var result = await _connection
                .Filter<Entity>()
                    .Eq(x => x.AccountId, entity.AccountId)
                    .Eq(x => x.Id, entity.Id)
                    .ElemMatchBuilder(
                        x => x.Identities,
                        f => f.Eq(i => i.IdentityProviderId, externalIdentity.IdentityProviderId)
                            .Eq(x => x.ExternalId, externalIdentity.ExternalId)
                    )
                .Update
                    .Set($"{nameof(Entity.Identities)}.$.{nameof(EntityIdentity.ExternalIdentity)}.{nameof(ExternalIdentity.Token)}", externalIdentity.ExternalIdentity.Token)
                .UpdateAndGetOneAsync();

            return result != null;
        }

        public async Task<bool> UpdateValueAsync(ExternalIdentity identity)
        {
            var result = await _connection
                .Filter<Entity>()
                    .ElemMatchBuilder(
                        x => x.Identities,
                        f => f
                            .Eq(i => i.IdentityProviderId, identity.Provider.ToString())
                            .Eq(x => x.ExternalId, identity.ExternalId)
                    )
                .Update
                    .Set($"{nameof(Entity.Identities)}.$.{nameof(EntityIdentity.ExternalIdentity)}", identity)
                .UpdateManyAsync();

            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateDataAsync(IEntityContext context, Guid entityId, EntityIdentity identity, Dictionary<string, object> data)
        {
            if (data != null)
            {
                // remove any nulls
                data = new Dictionary<string, object>(data.Where(x => x.Value != null));
            }

            var result = await _connection
                .Filter<Entity>()
                    .Eq(x => x.Id, entityId)
                    .ElemMatchBuilder(
                        x => x.Identities,
                        f => f.Eq(i => i.Id, identity.Id)
                    )
                .Update
                    .Set($"{nameof(Entity.Identities)}.$.{nameof(EntityIdentity.Data)}", data)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, context.Actor())
                .UpdateOneAsync();

            return result.MatchedCount == 1;
        }

        public Task<(IEntity, EntityIdentity)> FindAsync(IEntityContext context, ExternalProvider provider, string externalId)
        {
            return FindAsync(context, provider.ToString(), externalId);
        }

        public async Task<(IEntity, EntityIdentity)> FindAsync(IEntityContext context, string provider, string externalId)
        {
            var query = _connection.Filter<Entity>()
                .ElemMatchBuilder(
                    x => x.Identities,
                    f => f.Eq(i => i.IdentityProviderId, provider).Eq(x => x.ExternalId, externalId)
                );

            switch (context.Role)
            {
                case EntityRoleId.Root:
                    break;
                case EntityRoleId.Account:
                case EntityRoleId.Admin:
                    query.Eq(x => x.AccountId, context.AccountId.Value);
                    break;
                default:
                    throw new Exception($"{context.Role} not authorized");
            }

            var entity = await query.FirstOrDefaultAsync();
            var result = entity?.Identities.FirstOrDefault(i => string.Equals(i.IdentityProviderId, provider)
                && string.Equals(i.ExternalId, externalId));
            if (result == null) return (null, null);

            return (entity, result);
        }

        public async Task<IEnumerable<EntityIdentity>> GetByEntityAsync(Guid entityId)
        {
            var entity = await _connection.Filter<Entity>().Eq(x => x.Id, entityId).FirstOrDefaultAsync();
            return entity?.Identities;
        }

        public async Task<IEnumerable<EntityIdentity>> GetByEntityAsync(Guid entityId, ExternalProvider provider)
        {
            var identities = await GetByEntityAsync(entityId);
            return identities?.Where(x => x.IdentityProviderId == provider.ToString());
        }

        public async Task<EntityIdentity> GetByIdAsync(Guid id)
        {
            var entity = await _connection.Filter<Entity>()
                .ElemMatchBuilder(
                    x => x.Identities,
                    f => f.Eq(i => i.Id, id)
                    // Builders<EntityIdentity>.Filter.Eq(i => i.Id, id)
                ).FirstOrDefaultAsync();

            var result = entity?.Identities.FirstOrDefault(i => i.Id == id);

            return result;
        }

        public async Task<IEnumerable<IEntity>> GetEntityTrunkAsync(IEntityContext context)
        {
            var entities = await _connection.Filter<Entity>()
                .In(x => x.Id, context.GetEntityIds())
                .FindAsync();

            return entities;
        }

        public async Task<IEnumerable<IEntity>> GetEntityTrunkAsync(Guid entityId)
        {
            var entity = await _connection.Filter<Entity>()
                .Eq(x => x.Id, entityId)
                .FirstOrDefaultAsync();

            if (entity == null) return null;

            return await GetEntityTrunkAsync(entity.Context);
        }

        [Obsolete]
        public async Task<IEnumerable<TrunkIdentity>> GetEntityTrunkIdentitiesAsync(Guid entityId)
        {
            var entity = await _connection.Filter<Entity>()
                .Eq(x => x.Id, entityId)
                .FirstOrDefaultAsync();

            if (entity == null) return null;

            var entities = await GetEntityTrunkAsync(entity.Context);

            var result = entities
                .SelectMany(e => e.GetIdentities().Select(i => new TrunkIdentity
                {
                    EntityId = e.Id,
                    Level = e.GetType().Name,
                    Name = e.Name,
                    IdentityProviderId = i.IdentityProviderId,
                    ExternalId = i.ExternalId,
                })
            );

            return result;
        }
    }
}