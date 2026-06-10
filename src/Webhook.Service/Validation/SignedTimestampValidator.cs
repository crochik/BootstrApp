using System.Security.Cryptography;
using System.Text;
using Webhook.Service.Configuration;
using Webhook.Service.Engine;

namespace Webhook.Service.Validation;

/// <summary>
/// Validates signature schemes that carry a timestamp inside the signature header
/// and sign <c>{timestamp}.{body}</c> with HMAC-SHA256. Three header layouts are
/// supported via the registered type name:
/// <list type="bullet">
/// <item><c>stripe</c> – <c>Stripe-Signature: t=...,v1=...</c>, hex signature.</item>
/// <item><c>docuseal</c> – <c>X-Docuseal-Signature: {timestamp}.{signature}</c>, hex.</item>
/// <item><c>openphone</c> – <c>openphone-signature: hmac;1;{timestamp};{base64sig}</c>,
/// base64 signature, base64-decoded signing key.</item>
/// </list>
/// </summary>
public sealed class SignedTimestampValidator : IWebhookValidator
{
    private readonly string _scheme;

    public SignedTimestampValidator(string scheme) => _scheme = scheme;

    public string Type => _scheme;

    public ValidationResult Validate(WebhookContext context, AuthConfig config)
    {
        if (string.IsNullOrEmpty(config.Secret))
        {
            return ValidationResult.Fail($"{_scheme}: no secret configured");
        }

        var headerName = config.Header ?? DefaultHeader();
        var raw = context.GetHeader(headerName);
        if (string.IsNullOrEmpty(raw))
        {
            return ValidationResult.Fail($"{_scheme}: missing signature header '{headerName}'");
        }

        if (!TryParse(raw, out var timestamp, out var providedSignature))
        {
            return ValidationResult.Fail($"{_scheme}: malformed signature header");
        }

        var signingString = $"{timestamp}.{context.BodyText}";
        byte[] key;
        try
        {
            key = _scheme == "openphone"
                ? Convert.FromBase64String(config.Secret)
                : Encoding.UTF8.GetBytes(config.Secret);
        }
        catch (FormatException)
        {
            return ValidationResult.Fail($"{_scheme}: secret is not valid base64");
        }

        byte[] hash;
        using (var hmac = new HMACSHA256(key))
        {
            hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingString));
        }

        var expected = _scheme == "openphone"
            ? Convert.ToBase64String(hash)
            : Convert.ToHexString(hash).ToLowerInvariant();

        var match = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedSignature), Encoding.UTF8.GetBytes(expected));

        return match ? ValidationResult.Ok() : ValidationResult.Fail($"{_scheme}: signature mismatch");
    }

    private string DefaultHeader() => _scheme switch
    {
        "stripe" => "Stripe-Signature",
        "docuseal" => "X-Docuseal-Signature",
        "openphone" => "openphone-signature",
        _ => "X-Signature"
    };

    private bool TryParse(string header, out string timestamp, out string signature)
    {
        timestamp = string.Empty;
        signature = string.Empty;

        switch (_scheme)
        {
            case "stripe":
                foreach (var part in header.Split(','))
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length != 2) continue;
                    var k = kv[0].Trim();
                    if (k == "t") timestamp = kv[1].Trim();
                    else if (k == "v1") signature = kv[1].Trim();
                }
                return timestamp.Length > 0 && signature.Length > 0;

            case "docuseal":
            {
                var dot = header.IndexOf('.');
                if (dot <= 0 || dot == header.Length - 1) return false;
                timestamp = header[..dot];
                signature = header[(dot + 1)..];
                return true;
            }

            case "openphone":
            {
                var fields = header.Split(';');
                if (fields.Length < 4) return false;
                timestamp = fields[2];
                signature = fields[3];
                return timestamp.Length > 0 && signature.Length > 0;
            }

            default:
                return false;
        }
    }
}
