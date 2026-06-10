using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface ILeadTypeIntegrationAdapter
    {
        Task<IEnumerable<LeadTypeIntegration>> GetAsync(Guid leadTypeId);
        Task<LeadTypeIntegration> GetByIdAsync(Guid leadTypeId, Guid integrationId);
        Task<LeadTypeIntegration> AddOrUpdateAsync(LeadTypeIntegration record);
        Task<bool> DeleteAsync(Guid id, string serviceName);
    }
}