using Ingress.Configuration;
using Ingress.Engine;

namespace Ingress.Validation;

/// <summary>Result of a single validation step.</summary>
public readonly record struct ValidationResult(bool Succeeded, string? Reason)
{
    public static ValidationResult Ok() => new(true, null);
    public static ValidationResult Fail(string reason) => new(false, reason);
}

/// <summary>
/// A single authentication / validation strategy. Implementations are stateless
/// and receive their per-webhook configuration on each call.
/// </summary>
public interface IWebhookValidator
{
    /// <summary>Config discriminator this validator handles (e.g. "hmac").</summary>
    string Type { get; }

    ValidationResult Validate(WebhookContext context, AuthConfig config);
}
