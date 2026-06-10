namespace Webhook.Service.Engine;

/// <summary>
/// Outcome of processing a webhook. A handler may return a default result (let the
/// configured response apply) or override the status / content type / body.
/// </summary>
public sealed class WebhookResult
{
    /// <summary>When set, overrides the configured success status code.</summary>
    public int? StatusOverride { get; init; }

    /// <summary>When set, overrides the configured response content type.</summary>
    public string? ContentTypeOverride { get; init; }

    /// <summary>When set, overrides the configured response body (no token substitution).</summary>
    public string? BodyOverride { get; init; }

    /// <summary>A result that defers entirely to the configured response.</summary>
    public static WebhookResult Default { get; } = new();

    /// <summary>Convenience factory for a fully custom response.</summary>
    public static WebhookResult Custom(int status, string body, string contentType = "text/plain") =>
        new() { StatusOverride = status, BodyOverride = body, ContentTypeOverride = contentType };
}
