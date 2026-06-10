using Microsoft.Extensions.Logging;
using Ingress.Engine;

namespace Ingress.Validation;

/// <summary>
/// Runs every validator configured for a webhook. All must pass (logical AND).
/// An empty auth list, or an entry of type <c>none</c>, is treated as "no auth".
/// </summary>
public sealed class WebhookValidationPipeline
{
    private readonly IReadOnlyDictionary<string, IWebhookValidator> _validators;
    private readonly ILogger<WebhookValidationPipeline> _logger;

    public WebhookValidationPipeline(IEnumerable<IWebhookValidator> validators,
        ILogger<WebhookValidationPipeline> logger)
    {
        _validators = validators.ToDictionary(v => v.Type, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    /// <summary>Returns (true, null) when all configured validators pass.</summary>
    public ValidationResult Validate(WebhookContext context)
    {
        foreach (var auth in context.Definition.Auth)
        {
            if (string.Equals(auth.Type, "none", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!_validators.TryGetValue(auth.Type, out var validator))
            {
                _logger.LogWarning("Webhook '{Uuid}' references unknown auth type '{Type}'",
                    context.Definition.Uuid, auth.Type);
                return ValidationResult.Fail($"unknown auth type '{auth.Type}'");
            }

            var result = validator.Validate(context, auth);
            if (!result.Succeeded)
            {
                _logger.LogInformation("Webhook '{Uuid}' validation failed: {Reason}",
                    context.Definition.Uuid, result.Reason);
                return result;
            }
        }

        return ValidationResult.Ok();
    }
}
