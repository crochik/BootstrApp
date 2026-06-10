using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class LeadTypeAdapter : ILeadTypeAdapter
    {
        private readonly MongoAdapter<LeadType, Guid> _adapter;
        private readonly MongoConnection _connection;

        public LeadTypeAdapter(MongoConnection connection)
        {
            this._adapter = new MongoAdapter<LeadType, Guid>(connection);
            this._connection = connection;
        }

        public async Task<LeadType> CreateAsync(LeadType obj)
            => await _adapter.MapCreateAsync(obj);

        public Task<bool> DeleteAsync(Guid id)
            => _adapter.DeleteAsync(id);

        public async Task<IEnumerable<LeadType>> GetForEntityAsync(IEntityContext context)
            => await _connection.Filter<LeadType>()
                .Eq(x => x.EntityId, context.EntityId)
                .FindAsync();

        public async Task<IEnumerable<LeadType>> GetTrunkAsync(IEntityContext context)
            => await _connection.Filter<LeadType>()
                .In(x => x.EntityId, context.GetEntityIds())
                .FindAsync();

        public async Task<LeadType> GetByIdAsync(Guid id)
            => await _connection.GetByIdAsync<LeadType>(id);

        /// <summary>
        /// Only update name and flow id
        /// </summary>
        public async Task<bool> UpdateAsync(LeadType obj)
        {
            var dao = _connection.Map<LeadType>(obj);
            var result = await _connection.Filter<LeadType>()
                .Eq(x=>x.Id, obj.Id)
                .Update
                .Set(x=>x.FlowId, obj.FlowId)
                .Set(x=>x.Name, obj.Name)
                .UpdateAndGetOneAsync();

            return result != null;
        }

        public async Task<LeadType> UpdateSettingsAsync(Guid id, LeadTypeSettings mapping)
            => await _connection.UpdatePropertyAsync<LeadType, Guid, LeadTypeSettings>(id, x => x.Settings, mapping);

    }
}
