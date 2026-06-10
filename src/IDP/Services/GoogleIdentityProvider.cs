using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using PI.Shared.Models;

namespace Services;

public class GoogleIdentityProvider : AbstractIdentityProvider
{
    public override string Name => nameof(ExternalProvider.Google);

    public GoogleIdentityProvider(IMapper mapper) : base(mapper)
    {
    }

    public override ValueTask<ExternalIdentity> GetTenantAsync(ExternalLoginInfo loginInfo, ExternalIdentity userIdentity)
    {
        return ValueTask.FromResult<ExternalIdentity>(null);
    }
}