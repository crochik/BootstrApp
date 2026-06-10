using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface IAppointmentTypeAdapter // : IModelAdapter<AppointmentType>
    {
        /// <summary>
        /// Get appointment types for organization (get from context)
        /// </summary>
        Task<IEnumerable<AppointmentType>> GetForOrgAsync(IEntityContext context);

        /// <summary>
        /// Get default appointment type id for organization (get from context)
        /// It will return null if more than one is avaialble for it
        /// </summary>
        Task<AppointmentType> GetDefaultForOrgAsync(IEntityContext context, Guid? leadTypeId);

        Task<AppointmentType> GetByIdAsync(Guid id);
        Task<AppointmentType> CreateAsync(AppointmentType appointmentType);
        Task<bool> UpdateAsync(AppointmentType apptType);
    }
}