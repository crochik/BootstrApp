using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class EntityAdapter<TInt, T>
        where T : Entity, TInt
        where TInt : IEntity
    {
        protected readonly MongoConnection _connection;

        protected EntityAdapter(MongoConnection connection)
        {
            this._connection = connection;
        }

        public async Task<TInt> GetByIdAsync(Guid id)
            => await _connection.Filter<Entity, T>().Eq(x => x.Id, id).FirstOrDefaultAsync();

        public async Task<TInt> GetByIdAsync(IEntityContext context, Guid id)
        {
            var query = _connection.Filter<Entity, T>().Eq(x => x.Id, id);
            if (context.Role != EntityRoleId.Root) query.Eq(x => x.AccountId, context.AccountId.Value);

            var entity = await query.FirstOrDefaultAsync();
            if (entity != null)
            {
                if (!context.CanAccess(entity))
                {
                    // TODO: log? 
                    // ...
                    // return default(TInt);
                    throw new ForbiddenException(context);
                }
            }

            return entity;
        }

        public Task<bool> SetTimeZoneIdAsync(IEntityContext context, Guid entityId, string timeZoneId)
            => UpdatePropertyAsync(context, entityId, x=>x.TimeZoneId, timeZoneId);

        public async Task<bool> UpdatePropertyAsync<TField>(IEntityContext context, Guid entityId, Expression<Func<T, TField>> field, TField value)
        {
            var query = _connection.Filter<Entity, T>()
                .Eq(x => x.Id, entityId);
                
            if (context.Role != EntityRoleId.Root) query.Eq(x => x.AccountId, context.AccountId.Value);

            // TODO: enforce other roles?
            // ...

            var result = await query.Update
                .Set(field, value)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
                .UpdateOneAsync();

            return result.MatchedCount == 1;
        }

        public async Task<(IEntity Entity, EntityIdentity Identity)> AddAsync(IEntityContext context, Guid entityId, EntityIdentity identity)
        {
            var dao = _connection.Map<EntityIdentity>(identity);
            if (dao.Id == Guid.Empty) dao.Id = Guid.NewGuid();

            var entity = await _connection
                .Filter<Entity>().Eq(x => x.Id, entityId)
                .Update
                    .AddToSet(x => x.Identities, identity)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, context.Actor())
                    .UpdateAndGetOneAsync();

            var result = entity?.Identities?.FirstOrDefault(x => x.Id == dao.Id);

            return result == null ? (null, null) : (entity, result);
        }

        public Task<(TInt Entity, EntityIdentity Identity)> FindAsync(IEntityContext context, ExternalProvider provider, string externalId)
        {
            return FindAsync(context, provider.ToString(), externalId);
        }

        public async Task<(TInt Entity, EntityIdentity Identity)> FindAsync(IEntityContext context, string provider, string externalId)
        {
            var query = _connection.Filter<Entity, T>()
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
            if (result == null) return (default, null);

            return (entity, result);
        }
    }
}