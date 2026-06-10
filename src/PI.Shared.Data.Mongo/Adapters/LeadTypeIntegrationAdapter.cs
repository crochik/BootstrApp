using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class LeadTypeIntegrationAdapter : ILeadTypeIntegrationAdapter
    {
        private readonly MongoConnection _connection;

        public LeadTypeIntegrationAdapter(MongoConnection connection)
        {
            this._connection = connection;
        }

        public async Task<LeadTypeIntegration> AddOrUpdateAsync(LeadTypeIntegration model)
        {
            // TODO: handle update
            // ...
            
            // TODO: have to handle different integrations
            // ... 
            var dao = _connection.Map<LeadTypeIntegration>(model);

            var result = await _connection.Filter<LeadType>()
                .Eq(x => x.Id, model.LeadTypeId)
                .Update.Push(x => x.Integrations, dao)
                .UpdateAndGetOneAsync();

            return result.Integrations.FirstOrDefault(x => x.IntegrationId == model.IntegrationId);
        }

        public Task<bool> DeleteAsync(Guid id, string serviceName)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<LeadTypeIntegration>> GetAsync(Guid leadTypeId)
            => (await _connection.Filter<LeadType>()
                .Eq(x => x.Id, leadTypeId)
                .FirstOrDefaultAsync())?.Integrations ?? Array.Empty<LeadTypeIntegration>();

        public async Task<LeadTypeIntegration> GetByIdAsync(Guid leadTypeId, Guid integrationId)
            => (await GetAsync(leadTypeId)).FirstOrDefault(x => x.IntegrationId == integrationId);
    }
}