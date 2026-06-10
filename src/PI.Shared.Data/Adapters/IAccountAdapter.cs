using System;
using System.Threading.Tasks;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface IAccountAdapter 
    {
        Task<Account> CreateAsync(Account account, EntityIdentity entityIdentity=null);
        Task<(Account Entity, EntityIdentity Identity)> FindForIdentityAsync(string loginProvider, string externalId);
        Task<Account> GetByIdAsync(Guid value);
    }
}