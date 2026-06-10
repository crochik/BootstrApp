using System;
using System.Threading.Tasks;
using PI.Shared.Models;

namespace Services
{
    [Obsolete("moving away from Derived Events")]
    public interface ILeadEventService
    {     
        Task FireAsync(
            Guid eventId,
            Lead lead,
            string description,
            Guid? entityId = null,
            Appointment appointment = null,
            Guid? integrationId = null,
            string action = null,
            Guid? runId = null
        );
    }
}