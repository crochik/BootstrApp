using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Webhook.Integrations.Core.Catalog;
using Webhook.Integrations.Core.Configuration;
using Webhook.Integrations.Core.Subscriptions;

namespace Webhook.N8n.Controllers;

/// <summary>
/// Webhook-lifecycle endpoints for the n8n trigger node: <c>create</c> on workflow
/// activation, <c>delete</c> on deactivation, and <c>checkExists</c> to avoid
/// duplicate registrations. The samples endpoint lets the node pull example data.
/// </summary>
[ApiController]
[Route("n8n")]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly IEventCatalog _catalog;
    private readonly ISubscriptionStore _store;
    private readonly ISampleFactory _samples;
    private readonly IOptions<IntegrationOptions> _options;

    public SubscriptionsController(IEventCatalog catalog, ISubscriptionStore store, ISampleFactory samples, IOptions<IntegrationOptions> options)
    {
        _catalog = catalog;
        _store = store;
        _samples = samples;
        _options = options;
    }

    /// <summary>create: registers the node's webhook URL for an object/event.</summary>
    [HttpPost("subscriptions")]
    public IActionResult Create([FromBody] SubscribeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TargetUrl) ||
            !Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return BadRequest(new { error = "targetUrl must be an absolute http(s) URL" });
        }

        if (!_catalog.TryGetEvent(request.Object ?? "", request.Event ?? "", out _))
        {
            return BadRequest(new { error = $"unknown object/event '{request.Object}/{request.Event}'" });
        }

        var subscription = _store.Add(request.Object!, request.Event!, request.TargetUrl!);
        return Ok(new SubscribeResponse(subscription.Id, subscription.ObjectKey, subscription.EventKey, subscription.TargetUrl));
    }

    /// <summary>delete: removes a previously registered webhook.</summary>
    [HttpDelete("subscriptions/{id}")]
    public IActionResult Delete(string id)
    {
        // Idempotent: an already-removed hook still reports success so the node's
        // teardown never errors.
        _store.Remove(id);
        return NoContent();
    }

    /// <summary>
    /// checkExists: reports whether a webhook for this object/event/targetUrl is
    /// already registered (and its id), so the node can skip re-creating it.
    /// </summary>
    [HttpGet("subscriptions/exists")]
    public IActionResult Exists([FromQuery] string @object, [FromQuery] string @event, [FromQuery] string targetUrl)
    {
        var match = _store.Find(@object ?? "", @event ?? "")
            .FirstOrDefault(s => string.Equals(s.TargetUrl, targetUrl, StringComparison.OrdinalIgnoreCase));

        return Ok(new ExistsResponse(match is not null, match?.Id));
    }

    /// <summary>
    /// Returns a single representative sample (the delivered envelope) so the node can
    /// show example data when the user pins/test the trigger. Returns an array.
    /// </summary>
    [HttpGet("objects/{objectKey}/events/{eventKey}/samples")]
    public IActionResult Samples(string objectKey, string eventKey)
    {
        if (!_catalog.TryGetObject(objectKey, out var obj) ||
            !_catalog.TryGetEvent(objectKey, eventKey, out _))
        {
            return NotFound(new { error = $"unknown object/event '{objectKey}/{eventKey}'" });
        }

        return Ok(new[] { _samples.CreateDeliveredSample(obj, eventKey, _options.Value.Tenant) });
    }
}
