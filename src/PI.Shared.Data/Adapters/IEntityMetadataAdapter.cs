using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface IEntityMetadataAdapter
    {
        Task<IEnumerable<IEntityMetadata>> GetAsync(Guid partitionId, Guid entityId, string key);
        Task<IEnumerable<IEntityMetadata>> FindAsync(Guid partitionId, string key, string value);
        Task<IEnumerable<IEntityMetadata>> GetAsync(IEntityContext context);
        Task<IEnumerable<IEntityMetadata>> GetForEntityAsync(Guid organizationId);
        Task<IEnumerable<IEntityMetadata>> AddForEntityAsync(Guid organizationId, IEntityMetadata[] list, bool exclusive = true);
        Task<IEnumerable<IEntityMetadata>> DeleteAsync(IEntityMetadata[] list);
    }
}