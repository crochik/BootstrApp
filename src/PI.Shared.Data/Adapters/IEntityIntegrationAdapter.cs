using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface IEntityIntegrationAdapter
    {
        Task<IEnumerable<EntityIntegration>> GetForUserAsync(IEntityContext context);
        Task<EntityIntegration> FindForEntityAsync(Guid entityId, Guid integrationId);
        Task<IEnumerable<IEntityTrunkIntegration>> GetTrunkByIdAsync(Guid entityId, Guid integrationId);
        Task<EntityIntegration> AddOrUpdateAsync(Guid entityId, EntityIntegration entityIntegrationDAO);
        Task<IEnumerable<EntityIntegration>> GetAsync(IEntityContext context);
        Task<bool> DeleteAsync(IEntityContext context, string serviceName);
    }
}