using System.Security.Cryptography;
using System.Text;
using Ingress.Configuration;
using Ingress.Engine;

namespace Ingress.Validation;

/// <summary>
/// Validates an HMAC signature computed over the raw request body, as used by
/// GitHub (<c>X-Hub-Signature-256</c>), Shopify, Stripe and many others.
/// Supports sha256/sha1, hex/base64 encoding and an optional header prefix.
/// </summary>
public sealed class HmacSignatureValidator : IWebhookValidator
{
    public string Type => "hmac";

    public ValidationResult Validate(WebhookContext context, AuthConfig config)
    {
        if (string.IsNullOrEmpty(config.Secret))
        {
            return ValidationResult.Fail("hmac: no secret configured");
        }

        var headerName = config.Header;
        if (string.IsNullOrEmpty(headerName))
        {
            return ValidationResult.Fail("hmac: no signature header configured");
        }

        var provided = context.GetHeader(headerName);
        if (string.IsNullOrEmpty(provided))
        {
            return ValidationResult.Fail($"hmac: missing signature header '{headerName}'");
        }

        if (!string.IsNullOrEmpty(config.Prefix) && provided.StartsWith(config.Prefix, StringComparison.Ordinal))
        {
            provided = provided[config.Prefix.Length..];
        }

        // Default to signing the exact raw body bytes. When a template is given
        // (e.g. Slack's "v0:{timestamp}:{body}"), build the signing string from it.
        var message = BuildMessage(context, config);

        byte[] computed;
        using (var hmac = CreateHmac(config.Algorithm, config.Secret, config.SecretIsBase64))
        {
            computed = hmac.ComputeHash(message);
        }

        var expected = Encode(computed, config.Encoding);

        // Constant-time comparison over the bytes of the textual signatures.
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var match = CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);

        return match ? ValidationResult.Ok() : ValidationResult.Fail("hmac: signature mismatch");
    }

    private static byte[] BuildMessage(WebhookContext context, AuthConfig config)
    {
        if (string.IsNullOrEmpty(config.Template))
        {
            return context.RawBody;
        }

        var timestamp = string.IsNullOrEmpty(config.TimestampHeader)
            ? string.Empty : context.GetHeader(config.TimestampHeader);
        var signingString = config.Template
            .Replace("{timestamp}", timestamp, StringComparison.Ordinal)
            .Replace("{body}", context.BodyText, StringComparison.Ordinal);

        return Encoding.UTF8.GetBytes(signingString);
    }

    private static HMAC CreateHmac(string algorithm, string secret, bool secretIsBase64)
    {
        var key = secretIsBase64 ? Convert.FromBase64String(secret) : Encoding.UTF8.GetBytes(secret);
        return algorithm.ToLowerInvariant() switch
        {
            "sha1" => new HMACSHA1(key),
            "sha256" => new HMACSHA256(key),
            "sha512" => new HMACSHA512(key),
            _ => throw new NotSupportedException($"Unsupported HMAC algorithm '{algorithm}'")
        };
    }

    private static string Encode(byte[] data, string encoding) =>
        encoding.ToLowerInvariant() switch
        {
            "base64" => Convert.ToBase64String(data),
            "hex" => Convert.ToHexString(data).ToLowerInvariant(),
            _ => throw new NotSupportedException($"Unsupported signature encoding '{encoding}'")
        };
}
