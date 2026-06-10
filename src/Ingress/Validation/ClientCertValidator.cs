using Ingress.Configuration;
using Ingress.Engine;

namespace Ingress.Validation;

/// <summary>
/// Validates a mutual-TLS client certificate against an allowlist of SHA-1
/// thumbprints and/or a required subject. Used by callers that authenticate with
/// client certificates rather than a signature header (e.g. Kubernetes admission
/// webhooks and Salesforce outbound messages).
/// <para>
/// The server (Kestrel / reverse proxy) must be configured to negotiate client
/// certificates for <see cref="WebhookContext.ClientCertificate"/> to be present;
/// otherwise this validator fails closed.
/// </para>
/// </summary>
public sealed class ClientCertValidator : IWebhookValidator
{
    public string Type => "clientCert";

    public ValidationResult Validate(WebhookContext context, AuthConfig config)
    {
        var cert = context.ClientCertificate;
        if (cert is null)
        {
            return ValidationResult.Fail("clientCert: no client certificate presented");
        }

        if (config.Thumbprints.Count > 0)
        {
            var presented = Normalize(cert.Thumbprint);
            var allowed = config.Thumbprints.Any(t => Normalize(t) == presented);
            if (!allowed)
            {
                return ValidationResult.Fail("clientCert: thumbprint not allowed");
            }
        }

        if (!string.IsNullOrEmpty(config.Subject) &&
            !string.Equals(cert.Subject, config.Subject, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Fail("clientCert: subject mismatch");
        }

        if (config.Thumbprints.Count == 0 && string.IsNullOrEmpty(config.Subject))
        {
            return ValidationResult.Fail("clientCert: no thumbprints or subject configured");
        }

        return ValidationResult.Ok();
    }

    private static string Normalize(string thumbprint) =>
        thumbprint.Replace(":", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
}
