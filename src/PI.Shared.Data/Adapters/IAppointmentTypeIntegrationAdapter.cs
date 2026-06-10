using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface IAppointmentTypeIntegrationAdapter
    {
        Task<IEnumerable<AppointmentTypeIntegration>> GetAsync(Guid appointmentTypeId);
        Task<AppointmentTypeIntegration> GetByIdAsync(Guid appointmentTypeId, Guid integrationId);
        Task<AppointmentTypeIntegration> AddOrUpdateAsync(AppointmentTypeIntegration record);
        Task<bool> DeleteAsync(Guid id, string serviceName);
    }
}