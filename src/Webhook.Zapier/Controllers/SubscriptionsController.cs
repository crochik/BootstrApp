using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Webhook.Integrations.Core.Catalog;
using Webhook.Integrations.Core.Configuration;
using Webhook.Integrations.Core.Subscriptions;

namespace Webhook.Zapier.Controllers;

/// <summary>
/// REST Hook endpoints. Zapier subscribes when a user turns a Zap on and unsubscribes
/// when they turn it off. The samples endpoint backs Zapier's "test trigger" step.
/// </summary>
[ApiController]
[Route("zapier")]
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

    /// <summary>Subscribe: registers Zapier's callback URL for an object/event.</summary>
    [HttpPost("subscriptions")]
    public IActionResult Subscribe([FromBody] SubscribeRequest request)
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

    /// <summary>Unsubscribe: removes a previously registered callback.</summary>
    [HttpDelete("subscriptions/{id}")]
    public IActionResult Unsubscribe(string id)
    {
        // Idempotent: an already-removed hook still reports success so Zapier's
        // teardown never errors.
        _store.Remove(id);
        return NoContent();
    }

    /// <summary>
    /// "Perform list": returns a single representative sample so Zapier can pull
    /// example data when the user tests the trigger. Zapier expects an array.
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
