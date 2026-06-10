using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface IEntityIdentityAdapter
    {
        Task<IEntity> GetEntityByIdAsync(Guid entityId);
        Task<(IEntity, EntityIdentity)> FindAsync(IEntityContext context, string provider, string externalId);
        Task<(IEntity, EntityIdentity)> FindAsync(IEntityContext context, ExternalProvider provider, string externalId);
        Task<IEnumerable<EntityIdentity>> GetByEntityAsync(Guid entityId);
        Task<IEnumerable<EntityIdentity>> GetByEntityAsync(Guid entityId, ExternalProvider provider);
        
        [Obsolete]
        Task<IEnumerable<TrunkIdentity>> GetEntityTrunkIdentitiesAsync(Guid entityId);
        
        Task<EntityIdentity> AddAsync(IEntityContext context, Guid entityId, EntityIdentity identity);

        Task<bool> UpdateDataAsync(IEntityContext context, Guid entityId, EntityIdentity identity, Dictionary<string, object> map);
        Task<IEnumerable<IEntity>> GetEntityTrunkAsync(Guid entityId);
        
        [Obsolete]
        Task<bool> UpdateValueAsync(ExternalIdentity identity);
        Task<bool> UpdateTokenAsync(IEntity entity, EntityIdentity identity);
    }
}