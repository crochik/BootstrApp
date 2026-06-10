using System.Security.Cryptography;
using System.Text;
using Ingress.Configuration;
using Ingress.Engine;

namespace Ingress.Validation;

/// <summary>Validates HTTP Basic credentials from the <c>Authorization</c> header.</summary>
public sealed class BasicAuthValidator : IWebhookValidator
{
    public string Type => "basic";

    public ValidationResult Validate(WebhookContext context, AuthConfig config)
    {
        var auth = context.GetHeader("Authorization");
        const string scheme = "Basic ";
        if (!auth.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Fail("basic: missing or malformed Authorization header");
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(auth[scheme.Length..].Trim()));
        }
        catch (FormatException)
        {
            return ValidationResult.Fail("basic: credentials are not valid base64");
        }

        var separator = decoded.IndexOf(':');
        if (separator < 0)
        {
            return ValidationResult.Fail("basic: malformed credentials");
        }

        var username = decoded[..separator];
        var password = decoded[(separator + 1)..];

        var ok = FixedTimeEquals(username, config.Username ?? string.Empty)
                 & FixedTimeEquals(password, config.Password ?? string.Empty);

        return ok ? ValidationResult.Ok() : ValidationResult.Fail("basic: credential mismatch");
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
