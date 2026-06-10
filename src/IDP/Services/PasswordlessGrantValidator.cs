using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Validation;

namespace Services;

public class PasswordlessGrantValidator(PasswordlessService service) : IExtensionGrantValidator
{
    public string GrantType => PasswordlessService.GrantType;

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        var raw = context.Request.Raw;
        var clientId = context.Request.Client?.ClientId;
        var email = raw.Get("email");
        var phone = raw.Get("phone");
        var pin = raw.Get("pin");
        var verifier = raw.Get("code_verifier");

        var result = await service.ValidateAndConsumeAsync(clientId, email, phone, verifier, pin);
        if (!result.IsSuccess)
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                errorDescription: result.Status);
            return;
        }

        context.Result = new GrantValidationResult(
            subject: result.Value.ToString(),
            authenticationMethod: PasswordlessService.GrantType);
    }
}
