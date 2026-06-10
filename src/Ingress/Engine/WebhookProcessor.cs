using Microsoft.Extensions.Logging;
using Ingress.Formats;
using Ingress.Handlers;
using Ingress.Responses;
using Ingress.Validation;

namespace Ingress.Engine;

/// <summary>
/// Orchestrates a single webhook delivery: validate → registration handshake →
/// parse → handle → build response. All behaviour is driven by the
/// <see cref="WebhookContext.Definition"/>, so the pipeline is identical for
/// every third party.
/// </summary>
public sealed class WebhookProcessor
{
    private readonly WebhookValidationPipeline _validation;
    private readonly PayloadParserRegistry _parsers;
    private readonly IWebhookHandlerRegistry _handlers;
    private readonly ILogger<WebhookProcessor> _logger;

    public WebhookProcessor(
        WebhookValidationPipeline validation,
        PayloadParserRegistry parsers,
        IWebhookHandlerRegistry handlers,
        ILogger<WebhookProcessor> logger)
    {
        _validation = validation;
        _parsers = parsers;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task<BuiltResponse> ProcessAsync(WebhookContext context, CancellationToken cancellationToken = default)
    {
        var definition = context.Definition;

        // 1. Query-challenge handshake (unauthenticated verification ping, e.g. Meta
        //    hub.challenge / Microsoft Graph validationToken) — answered before auth.
        var queryHandshake = RegistrationHandshake.TryHandleQuery(context);
        if (queryHandshake is not null)
        {
            _logger.LogInformation("Webhook '{Uuid}' answered query registration handshake", definition.Uuid);
            return ResponseBuilder.Build(context, queryHandshake);
        }

        // 2. Authentication / validation — all configured steps must pass.
        var validation = _validation.Validate(context);
        if (!validation.Succeeded)
        {
            return new BuiltResponse(definition.Response.FailureStatus, "text/plain",
                validation.Reason ?? "unauthorized");
        }

        // 3. Body-challenge handshake (arrives signed, e.g. Slack url_verification).
        var bodyHandshake = RegistrationHandshake.TryHandleBody(context);
        if (bodyHandshake is not null)
        {
            _logger.LogInformation("Webhook '{Uuid}' answered body registration handshake", definition.Uuid);
            return ResponseBuilder.Build(context, bodyHandshake);
        }

        // 4. Parse the body per configured format.
        try
        {
            context.Payload = _parsers.Resolve(definition.Format).Parse(context);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or System.Xml.XmlException)
        {
            _logger.LogInformation("Webhook '{Uuid}' body failed to parse as {Format}: {Message}",
                definition.Uuid, definition.Format, ex.Message);
            return new BuiltResponse(400, "text/plain", $"invalid {definition.Format} body");
        }

        // 5. Dispatch to the configured handler.
        var handler = _handlers.Resolve(definition.Handler);
        if (handler is null)
        {
            _logger.LogError("Webhook '{Uuid}' references unknown handler '{Handler}'",
                definition.Uuid, definition.Handler);
            return new BuiltResponse(500, "text/plain", $"handler '{definition.Handler}' not registered");
        }

        var result = await handler.HandleAsync(context, cancellationToken);

        // 6. Build the response.
        return ResponseBuilder.Build(context, result);
    }
}
