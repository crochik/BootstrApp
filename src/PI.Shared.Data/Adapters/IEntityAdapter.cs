using System;
using System.Threading.Tasks;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface IEntityAdapter<TInt> where TInt : IEntity
    {
        Task<TInt> GetByIdAsync(Guid id);
        Task<TInt> GetByIdAsync(IEntityContext context, Guid id);
        Task<bool> SetTimeZoneIdAsync(IEntityContext context, Guid entityId, string timeZoneId);
        Task<(IEntity Entity, EntityIdentity Identity)> AddAsync(IEntityContext context, Guid entityId, EntityIdentity identity);
        Task<(TInt Entity, EntityIdentity Identity)> FindAsync(IEntityContext context, ExternalProvider provider, string externalId);
        Task<(TInt Entity, EntityIdentity Identity)> FindAsync(IEntityContext context, string provider, string externalId);
    }
}