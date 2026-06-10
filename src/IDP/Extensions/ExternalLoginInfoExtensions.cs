using System.Linq;
using System.Security.Claims;
using IdentityModel;
using Microsoft.AspNetCore.Identity;

namespace IDP.Data;

public static class ExternalLoginInfoExtensions
{
    private const string Microsoft = "Microsoft";
    private const string Google = "Google";
    private const string Okta = "Okta";

    public static string GetClaim(this ExternalLoginInfo info, string type) {
        return info.Principal.Claims.FirstOrDefault( c => c.Type.Equals(type))?.Value;
    }

    public static string GetUserName(this ExternalLoginInfo info)
    {
        // ????
        return info.GetEmail(); 
    }

    public static string GetEmail(this ExternalLoginInfo info)
    {
        switch (info.LoginProvider) {
            case Microsoft:
            case Google:
                return info.GetClaim(ClaimTypes.Email);

            case Okta:
                return info.GetClaim(JwtClaimTypes.PreferredUserName);

            default:
                // Generic OIDC/OAuth2 providers map their email claim to JwtClaimTypes.Email
                // via ClaimActions.MapJsonKey (see ProviderResolver.ApplyClaimMappings).
                return info.GetClaim(JwtClaimTypes.Email) ?? info.GetClaim(ClaimTypes.Email);
        }
    }

    public static string GetName(this ExternalLoginInfo info)
    {
        switch (info.LoginProvider) {
            case Microsoft:
            case Google:
                return info.GetClaim(ClaimTypes.Name);

            case Okta:
                return info.GetClaim(JwtClaimTypes.Name);

            default:
                return info.GetClaim(JwtClaimTypes.Name) ?? info.GetClaim(ClaimTypes.Name);
        }
    }

}