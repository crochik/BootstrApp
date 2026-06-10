using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class AppointmentTypeAdapter : IAppointmentTypeAdapter
    {
        private readonly MongoConnection _connection;

        public AppointmentTypeAdapter(MongoConnection connection)
        {
            this._connection = connection;
        }

        public async Task<IEnumerable<AppointmentType>> GetForOrgAsync(IEntityContext context)
        {
            if (!context.OrganizationId.HasValue) return null;

            var list = await _connection.Filter<AppointmentType>()
                .Eq(x => x.EntityId, context.OrganizationId.Value)
                .FindAsync();

            return list.Select(x => _connection.Map<EntityAppointmentType>(x));
        }

        public async Task<AppointmentType> GetDefaultForOrgAsync(IEntityContext context, Guid? leadTypeId)
        {
            var list = (await GetForOrgAsync(context)).ToList();
            switch (list.Count)
            {
                case 0: return null;
                case 1: return list[0];
                default: break;
            }

            // one for the leadtype
            if (leadTypeId.HasValue)
            {
                foreach (var a in list)
                {
                    if (a.LeadTypeId.HasValue)
                    {
                        if (a.LeadTypeId.Value == leadTypeId) return a;
                    }
                }
            }

            // TODO: how to handle when more than one per entity
            // add some "default" property to appointment type or 
            // a property on the organization
            // ...

            // hack for now, look for a "First Appointment"
            list = list.Where(x => string.Equals(x.Name, "First Appointment")).ToList();

            return list.Count == 1 ? list[0] : null;
        }

        public async Task<AppointmentType> GetByIdAsync(Guid id) => await _connection.GetByIdAsync<AppointmentType>(id);

        public async Task<AppointmentType> CreateAsync(AppointmentType appointmentType)
        {
            await _connection.InsertAsync<AppointmentType, AppointmentType>(appointmentType);

            return await GetByIdAsync(appointmentType.Id);
        }

        public async Task<bool> UpdateAsync(AppointmentType apptType)
        {
            var dao = _connection.Map<AppointmentType>(apptType);
            var updated = await _connection.ReplaceAsync<AppointmentType, Guid>(dao);
            return updated != null;
        }
    }
}
