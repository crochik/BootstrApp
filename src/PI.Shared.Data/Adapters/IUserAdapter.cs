using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface IUserAdapter : IEntityAdapter<User>
    {
        Task<IEnumerable<User>> GetAsync(IEntityContext context);
        Task<bool> DeleteAsync(Guid entityId);
        Task<IEnumerable<User>> GetAvaialbleForAppointmentAsync(Guid apptTypeId);
        Task<User> CreateAsync(IEntityContext context, User user, EntityIdentity identity = null);
        Task<User> FindForIdentityAsync(string loginProvider, string providerKey);
        Task<(User Entity, EntityIdentity Identity)> FindForIdentityAsync(IEntityContext context, string loginProvider, string providerKey);
        Task<IEnumerable<MergeUserCandidateMatch>> GetMergeCandidatesAsync(Guid accountId);
        Task<User> MergeAsync(User target, User other);
        Task<User> UpdateAsync(User user);
        Task<User> SetOrganization(IEntityContext context, User user, Guid id);
        Task<bool> UpdateIsActiveAsync(IEntityContext context, Guid id, bool isActive);
        Task<bool> UpdateExternalIdentiyAsync(User user, ExternalIdentity externalIdentity);
    }
}