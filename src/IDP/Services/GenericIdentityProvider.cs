using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using PI.Shared.Models;

namespace Services;

/// <summary>
/// Identity provider used when the AppClient's AuthenticationProviders entry has Type "oidc"/"oauth2"
/// (i.e. a tenant-defined provider, not one of the six built-ins). LoginService falls back to this
/// when the named provider isn't in its dictionary. Claim extraction is driven by the
/// ClaimActions.MapJsonKey calls wired in ProviderResolver for generic providers, so the ASP.NET
/// principal already carries the expected JwtClaimTypes — AutoMapper picks them up via the existing
/// ExternalLoginInfo -> ExternalIdentity profile.
/// </summary>
public class GenericIdentityProvider : AbstractIdentityProvider
{
    public const string ProviderName = "Generic";

    public override string Name => ProviderName;

    public GenericIdentityProvider(IMapper mapper) : base(mapper)
    {
    }

    public override ValueTask<ExternalIdentity> GetTenantAsync(ExternalLoginInfo loginInfo, ExternalIdentity userIdentity)
    {
        return ValueTask.FromResult<ExternalIdentity>(null);
    }
}
