using System.Security.Cryptography;
using System.Text;
using Ingress.Configuration;
using Ingress.Engine;

namespace Ingress.Validation;

/// <summary>
/// Validates a static token. Two flavours share this implementation:
/// <list type="bullet">
/// <item><c>bearer</c> – expects <c>Authorization: Bearer &lt;token&gt;</c>.</item>
/// <item><c>apikey</c> – expects the token verbatim in the configured header
/// (defaults to <c>X-Api-Key</c>).</item>
/// </list>
/// </summary>
public sealed class TokenHeaderValidator : IWebhookValidator
{
    private const string Bearer = "bearer";
    private readonly string _type;

    public TokenHeaderValidator(string type) => _type = type;

    public string Type => _type;

    public ValidationResult Validate(WebhookContext context, AuthConfig config)
    {
        if (string.IsNullOrEmpty(config.Token))
        {
            return ValidationResult.Fail($"{_type}: no token configured");
        }

        string presented;
        if (string.Equals(_type, Bearer, StringComparison.OrdinalIgnoreCase))
        {
            var auth = context.GetHeader("Authorization");
            const string scheme = "Bearer ";
            if (!auth.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Fail("bearer: missing or malformed Authorization header");
            }

            presented = auth[scheme.Length..].Trim();
        }
        else
        {
            var header = string.IsNullOrEmpty(config.Header) ? "X-Api-Key" : config.Header;
            presented = context.GetHeader(header);
            if (string.IsNullOrEmpty(presented))
            {
                return ValidationResult.Fail($"apikey: missing header '{header}'");
            }
        }

        return FixedTimeEquals(presented, config.Token)
            ? ValidationResult.Ok()
            : ValidationResult.Fail($"{_type}: token mismatch");
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
