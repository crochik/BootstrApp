using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using PI.Shared.Services;

namespace Services;

public class MagicCodeGrantValidator(
    MagicCodeService magicCodeService,
    AuthorizationService authorizationService) : IExtensionGrantValidator
{
    public string GrantType => "magic_code";

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        var clientId = context.Request.Client?.ClientId;
        var code = context.Request.Raw.Get("code");

        var result = await magicCodeService.GetAndValidateAsync(code, clientId);
        if (!result.IsSuccess)
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                errorDescription: result.Status);
            return;
        }

        var user = result.Value.User;
        var client = result.Value.Client;

        var profile = await authorizationService.GetProfileAsync(user, client);
        if (profile == null)
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                errorDescription: "Couldn't resolve profile");
            return;
        }

        context.Result = new GrantValidationResult(
            subject: user.Id.ToString(),
            authenticationMethod: "MagicCode");
    }
}
