using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface ILeadTypeAdapter  : IModelAdapter<LeadType>
    {
        Task<LeadType> UpdateSettingsAsync(Guid id, LeadTypeSettings mapping);
        Task<IEnumerable<LeadType>> GetForEntityAsync(IEntityContext context);
    }
}