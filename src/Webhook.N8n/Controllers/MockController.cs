using Microsoft.AspNetCore.Mvc;
using Webhook.Integrations.Core.Catalog;
using Webhook.Integrations.Core.Mock;

namespace Webhook.N8n.Controllers;

/// <summary>
/// Demo-only endpoint that fabricates an event and publishes it through the durable
/// pipeline so you can watch an n8n workflow fire end-to-end without a real domain.
/// In a production integration the equivalent call lives in your business code, not
/// an HTTP endpoint.
/// </summary>
[ApiController]
[Route("n8n/mock")]
public sealed class MockController : ControllerBase
{
    private readonly IEventCatalog _catalog;
    private readonly MockEventEmitter _emitter;

    public MockController(IEventCatalog catalog, MockEventEmitter emitter)
    {
        _catalog = catalog;
        _emitter = emitter;
    }

    /// <summary>Generates a sample payload for the object/event and publishes it to subscribers.</summary>
    [HttpPost("emit")]
    public async Task<IActionResult> Emit([FromBody] EmitRequest request, CancellationToken ct)
    {
        if (!_catalog.TryGetObject(request.Object ?? "", out var obj) ||
            !_catalog.TryGetEvent(request.Object!, request.Event ?? "", out _))
        {
            return BadRequest(new { error = $"unknown object/event '{request.Object}/{request.Event}'" });
        }

        var (enqueued, data) = await _emitter.EmitAsync(obj, request.Event!, ct);
        return Ok(new { enqueued, eventName = $"{obj.Key}.{request.Event}", data });
    }
}
