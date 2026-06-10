using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class IntegrationAppointmentAdapter : IIntegrationAppointmentAdapter
    {
        private readonly MongoConnection _connection;

        public IntegrationAppointmentAdapter(MongoConnection connection)
        {
            this._connection = connection;
        }

        public async Task<IIntegrationAppointment> AddAsync(IEntityContext context, IIntegrationAppointment model)
        {
            // TODO: have to handle different integrations
            // ... 
            var dao = _connection.Map<AppointmentIntegration>(model);

            var result = await _connection.Filter<Appointment>()
                .Eq(x => x.Id, model.AppointmentId)
                .Update
                    .Push(x => x.Integrations, dao)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, context.Actor())
                .UpdateAndGetOneAsync();

            return result.Integrations
                .FirstOrDefault(x => string.Equals(x.ExternalId, model.ExternalId) &&
                    x.IntegrationId == model.IntegrationId);
        }

        public async Task<(Appointment, IIntegrationAppointment)> FindAsync(Guid integrationId, string externalId)
        {
            var appointment = await _connection.Filter<Appointment>()
                .ElemMatchBuilder(x => x.Integrations,
                    f => f.Eq(i => i.ExternalId, externalId)
                        .Eq(i => i.IntegrationId, integrationId)
                    // Builders<AppointmentIntegration>.Filter.Eq(i => i.ExternalId, externalId) &
                    // Builders<AppointmentIntegration>.Filter.Eq(i => i.IntegrationId, integrationId)
                )
                .FirstOrDefaultAsync();

            var integration = appointment?.Integrations
                .FirstOrDefault(x => string.Equals(x.ExternalId, externalId) && x.IntegrationId == integrationId);

            if (integration == null) return (null, null);

            integration.AppointmentId = appointment.Id;
            return (appointment, integration);
        }

        public async Task<IEnumerable<IIntegrationAppointment>> GetAsync(Guid id)
            => (await _connection.Filter<Appointment>().Eq(x => x.Id, id)
                .FirstOrDefaultAsync())?.Integrations ??
                Array.Empty<IIntegrationAppointment>();

        public Task<bool> UpdateStatusAsync(Guid appointmentId, Guid integrationId, string externalId, string status, string url)
        {
            // TODO: update
            // ...

            throw new NotImplementedException();
        }

        public async Task<IIntegrationAppointment> UpsertAsync(IEntityContext context, IIntegrationAppointment model)
        {
            var existing = await _connection.Filter<Appointment>()
                .Eq(x => x.Id, model.AppointmentId)
                .ElemMatchBuilder(x => x.Integrations,
                    f => f.Eq(i => i.ExternalId, model.ExternalId)
                        .Eq(i => i.IntegrationId, model.IntegrationId)
                    // Builders<AppointmentIntegration>.Filter.Eq(i => i.ExternalId, model.ExternalId) &
                    // Builders<AppointmentIntegration>.Filter.Eq(i => i.IntegrationId, model.IntegrationId)
                ).FirstOrDefaultAsync();

            if (existing == null) return await AddAsync(context, model);

            // update
            var dao = _connection.Map<AppointmentIntegration>(model);

            // TODO: UPDATE
            // ... 

            throw new NotImplementedException();
        }
    }
}