using Microsoft.Extensions.Logging;
using Webhook.Service.Engine;

namespace Webhook.Service.Handlers;

/// <summary>
/// Default handler: logs that a delivery arrived (name, method, body size) and
/// defers to the configured response. Serves as a working example for custom handlers.
/// </summary>
public sealed class LoggingWebhookHandler : IWebhookHandler
{
    private readonly ILogger<LoggingWebhookHandler> _logger;

    public LoggingWebhookHandler(ILogger<LoggingWebhookHandler> logger) => _logger = logger;

    public string Name => "logging";

    public Task<WebhookResult> HandleAsync(WebhookContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Webhook '{Name}' ({Uuid}) received: {Method}, {Bytes} bytes, format={Format}",
            context.Definition.Name, context.Definition.Uuid, context.Method,
            context.RawBody.Length, context.Definition.Format);

        return Task.FromResult(WebhookResult.Default);
    }
}
