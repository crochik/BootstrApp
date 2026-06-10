using System.Security.Cryptography;
using System.Text;
using Ingress.Configuration;
using Ingress.Engine;

namespace Ingress.Validation;

/// <summary>
/// Validates an ECDSA (P-256 / SHA-256) signature over <c>{timestamp}{raw body}</c>,
/// as used by the SendGrid Event Webhook. The signature header
/// (<c>X-Twilio-Email-Event-Webhook-Signature</c>) carries a base64 ASN.1/DER
/// signature; the timestamp header
/// (<c>X-Twilio-Email-Event-Webhook-Timestamp</c>) is prefixed to the body before
/// hashing. <see cref="AuthConfig.PublicKey"/> is the base64 SubjectPublicKeyInfo
/// verification key issued by the provider.
/// </summary>
public sealed class EcdsaSignatureValidator : IWebhookValidator
{
    public string Type => "ecdsa";

    public ValidationResult Validate(WebhookContext context, AuthConfig config)
    {
        if (string.IsNullOrEmpty(config.PublicKey))
        {
            return ValidationResult.Fail("ecdsa: no public key configured");
        }

        var sigHeader = string.IsNullOrEmpty(config.Header)
            ? "X-Twilio-Email-Event-Webhook-Signature" : config.Header;
        var tsHeader = string.IsNullOrEmpty(config.TimestampHeader)
            ? "X-Twilio-Email-Event-Webhook-Timestamp" : config.TimestampHeader;

        var signature = context.GetHeader(sigHeader);
        if (string.IsNullOrEmpty(signature))
        {
            return ValidationResult.Fail($"ecdsa: missing signature header '{sigHeader}'");
        }

        var timestamp = context.GetHeader(tsHeader);
        if (string.IsNullOrEmpty(timestamp))
        {
            return ValidationResult.Fail($"ecdsa: missing timestamp header '{tsHeader}'");
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signature);
        }
        catch (FormatException)
        {
            return ValidationResult.Fail("ecdsa: signature is not valid base64");
        }

        // Signed content: timestamp followed by the raw request body bytes.
        var prefix = Encoding.UTF8.GetBytes(timestamp);
        var signed = new byte[prefix.Length + context.RawBody.Length];
        Buffer.BlockCopy(prefix, 0, signed, 0, prefix.Length);
        Buffer.BlockCopy(context.RawBody, 0, signed, prefix.Length, context.RawBody.Length);

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(config.PublicKey), out _);

            var verified = ecdsa.VerifyData(
                signed, signatureBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

            return verified
                ? ValidationResult.Ok()
                : ValidationResult.Fail("ecdsa: signature mismatch");
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return ValidationResult.Fail($"ecdsa: {ex.Message}");
        }
    }
}
