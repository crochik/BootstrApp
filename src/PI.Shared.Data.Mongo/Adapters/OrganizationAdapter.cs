using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class OrganizationAdapter : EntityAdapter<Organization, Organization>, IOrganizationAdapter
    {
        public OrganizationAdapter(MongoConnection connection) : base(connection)
        {
        }

        public async Task<Organization> CreateAsync(IEntityContext context, Organization organization, EntityIdentity identity = null)
        {
            var dao = _connection.Map<Organization>(organization);
            dao.AccountId = context.AccountId.Value;
            dao.EntityId = context.AccountId.Value;
            dao.CreatedOn = DateTime.UtcNow;
            dao.LastModifiedOn = DateTime.UtcNow;
            dao.LastActor = context.Actor();

            if (identity != null)
            {
                var iDao = _connection.Map<EntityIdentity>(identity);
                dao.Identities = new[] { iDao };
            }

            await _connection.InsertAsync<Entity>(dao);

            return dao;
        }

        public async Task<IEnumerable<Organization>> GetAsync(IEntityContext context)
        {
            switch (context.Role)
            {
                case EntityRoleId.Account:
                case EntityRoleId.Admin:
                    return await GetByAccountAsync(context.AccountId.Value);

                default:
                    return null;
            }
        }

        public async Task<IEnumerable<Organization>> GetByAccountAsync(Guid accountId, bool? isActive = true)
        {
            var query = _connection.Filter<Organization>()
                .Eq("_t", nameof(Organization))
                .Eq(x => x.AccountId, accountId);

            if (isActive.HasValue)
            {
                query.Eq(x => x.IsActive, isActive);
            }

            query
                .IncludeField(x => x.Id)
                .IncludeField(x => x.Name)
                .IncludeField(x => x.IsActive)
                .IncludeField(x => x.AccountId);

            return await query.FindAsync();
        }
    }
}