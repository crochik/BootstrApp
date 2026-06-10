using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using PI.Shared.Models;

namespace Services;

public interface IIdentityProvider
{
    string Name { get; }
    ValueTask<ExternalIdentity> GetIdentityAsync(ExternalLoginInfo loginInfo);
    ValueTask<ExternalIdentity> GetTenantAsync(ExternalLoginInfo loginInfo, ExternalIdentity userIdentity);
    User BuildUser(Account account, ExternalIdentity userIdentity);
}