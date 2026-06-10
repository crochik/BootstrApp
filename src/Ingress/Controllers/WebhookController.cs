using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Ingress.Config;
using Ingress.Engine;

namespace Ingress.Controllers;

/// <summary>
/// Single dynamic entry point for all inbound webhooks. The <c>{uuid}</c> route segment
/// selects the configured <c>WebhookDefinition</c>; everything else is driven by
/// that configuration. Accepts GET (for verification handshakes) and POST/PUT
/// (for deliveries).
/// </summary>
[ApiController]
[Route("ingress/{uuid}")]
public sealed class WebhookController : ControllerBase
{
    private readonly IWebhookConfigStore _store;
    private readonly WebhookProcessor _processor;

    public WebhookController(IWebhookConfigStore store, WebhookProcessor processor)
    {
        _store = store;
        _processor = processor;
    }

    [HttpGet]
    [HttpPost]
    [HttpPut]
    public async Task<IActionResult> Receive(string uuid, CancellationToken cancellationToken)
    {
        var definition = await _store.GetByUuidAsync(uuid, cancellationToken);
        if (definition is null)
        {
            return NotFound();
        }

        // Capture the exact raw body bytes; required for HMAC verification.
        // Rewind first in case MVC already read the stream (e.g. form binding).
        if (Request.Body.CanSeek)
        {
            Request.Body.Position = 0;
        }

        byte[] rawBody;
        using (var ms = new MemoryStream())
        {
            await Request.Body.CopyToAsync(ms, cancellationToken);
            rawBody = ms.ToArray();
        }

        var context = new WebhookContext
        {
            Definition = definition,
            Method = Request.Method,
            RawBody = rawBody,
            Headers = Request.Headers.ToDictionary(
                h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            Query = Request.Query.ToDictionary(
                q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            RemoteIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            RequestUrl = Request.GetEncodedUrl(),
            ClientCertificate = HttpContext.Connection.ClientCertificate
        };

        var response = await _processor.ProcessAsync(context, cancellationToken);

        return new ContentResult
        {
            StatusCode = response.Status,
            ContentType = response.ContentType,
            Content = response.Body
        };
    }
}
