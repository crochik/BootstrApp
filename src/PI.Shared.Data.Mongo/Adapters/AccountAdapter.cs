using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class AccountAdapter : IAccountAdapter
    {
        private readonly MongoConnection _connection;
        public AccountAdapter(MongoConnection connection)
        {
            this._connection = connection;
        }

        public async Task<Account> CreateAsync(Account model, EntityIdentity entityIdentity = null)
        {
            var dao = _connection.Map<Account>(model);
            dao.CreatedOn = DateTime.UtcNow;
            dao.LastModifiedOn = DateTime.UtcNow;

            if (entityIdentity != null)
            {
                var iDao = _connection.Map<EntityIdentity>(entityIdentity);
                dao.Identities = new[] { iDao };
            }

            await _connection.InsertAsync<Entity>(dao);
            return dao;
        }

        public async Task<Account> GetByIdAsync(Guid id)
            => await _connection.Filter<Account>().Eq(x => x.Id, id).FirstOrDefaultAsync();

        public async Task<(Account Entity, EntityIdentity Identity)> FindForIdentityAsync(string loginProvider, string externalId)
        {
            var account = await _connection.Filter<Account>()
                .Eq("_t", nameof(Account))
                .ElemMatchBuilder(x => x.Identities,
                    f => f.Eq(i => i.IdentityProviderId, loginProvider)
                        .Eq(i => i.ExternalId, externalId)
                ).FirstOrDefaultAsync();

            var identity = account?.FindIdentity(loginProvider, externalId);
            return identity != null ? (account, identity) : (null, null);
        }
    }
}