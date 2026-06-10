using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class AppointmentTypeIntegrationAdapter : IAppointmentTypeIntegrationAdapter
    {
        private readonly MongoConnection _connection;
        private readonly IIntegrationAdapter _integrationAdapter;

        public AppointmentTypeIntegrationAdapter(
            MongoConnection connection,
            IIntegrationAdapter integrationAdapter)
        {
            this._connection = connection;
            this._integrationAdapter = integrationAdapter;
        }

        public async Task<AppointmentTypeIntegration> AddOrUpdateAsync(AppointmentTypeIntegration model)
        {
            // TODO: handle update
            // ...

            // TODO: have to handle different integrations
            // ... 
            var dao = _connection.Map<AppointmentTypeIntegration>(model);

            var result = await _connection.Filter<AppointmentType>()
                .Eq(x => x.Id, model.AppointmentTypeId)
                .Update.Push(x => x.Integrations, dao)
                .UpdateAndGetOneAsync();

            return result.Integrations.FirstOrDefault(x => x.IntegrationId == model.IntegrationId);
        }

        public Task<bool> DeleteAsync(Guid id, string serviceName)
        {
            var integration = _integrationAdapter.GetByServiceName(serviceName);

            //... 

            throw new NotImplementedException();
        }

        public async Task<IEnumerable<AppointmentTypeIntegration>> GetAsync(Guid appointmentTypeId)
            => (await _connection.Filter<AppointmentType>()
                .Eq(x => x.Id, appointmentTypeId)
                .FirstOrDefaultAsync())?.Integrations ?? Array.Empty<AppointmentTypeIntegration>();

        public async Task<AppointmentTypeIntegration> GetByIdAsync(Guid appointmentTypeId, Guid integrationId)
            => (await GetAsync(appointmentTypeId)).FirstOrDefault(x => x.IntegrationId == integrationId);
    }
}