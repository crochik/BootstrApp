using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Integrations.Catalog;
using PI.Shared.Integrations.Subscriptions;

namespace N8n.Controllers;

/// <summary>
/// Webhook-lifecycle endpoints for the n8n trigger node: <c>create</c> on workflow
/// activation, <c>delete</c> on deactivation, and <c>checkExists</c> to avoid duplicate
/// registrations. The samples endpoint lets the node pull example data.
/// </summary>
[Authorize("n8n")]
[Route("/n8n/v1")]
public class SubscriptionsController : APIController
{
    private readonly ILogger<SubscriptionsController> _logger;
    private readonly IObjectCatalog _catalog;
    private readonly ISubscriptionStore _store;
    private readonly ISampleFactory _samples;

    public SubscriptionsController(
        ILogger<SubscriptionsController> logger,
        IObjectCatalog catalog,
        ISubscriptionStore store,
        ISampleFactory samples)
    {
        _logger = logger;
        _catalog = catalog;
        _store = store;
        _samples = samples;
    }

    /// <summary>create: registers the node's webhook URL for an object/event.</summary>
    [HttpPost("subscriptions")]
    public async Task<IActionResult> CreateAsync([FromBody] SubscribeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TargetUrl) ||
            !Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return BadRequest(new { error = "targetUrl must be an absolute http(s) URL" });
        }

        if (await _catalog.GetEventAsync(Context, request.Object ?? "", request.Event ?? "") is null)
        {
            return BadRequest(new { error = $"unknown object/event '{request.Object}/{request.Event}'" });
        }

        var subscription = await _store.AddAsync(Context, request.Object, request.Event, request.TargetUrl);

        _logger.LogInformation("Created n8n subscription {SubscriptionId} for {ObjectType}/{Event}",
            subscription.Id, request.Object, request.Event);

        return Ok(new SubscribeResponse(subscription.Id.ToString(), request.Object, request.Event, subscription.Url));
    }

    /// <summary>delete: removes a previously registered webhook. Idempotent.</summary>
    [HttpDelete("subscriptions/{id}")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        if (Guid.TryParse(id, out var subscriptionId))
        {
            await _store.RemoveAsync(Context, subscriptionId);
        }

        return NoContent();
    }

    /// <summary>
    /// checkExists: reports whether a webhook for this object/event/targetUrl is already
    /// registered (and its id), so the node can skip re-creating it.
    /// </summary>
    [HttpGet("subscriptions/exists")]
    public async Task<IActionResult> ExistsAsync([FromQuery] string @object, [FromQuery] string @event, [FromQuery] string targetUrl)
    {
        var matches = await _store.FindAsync(Context, @object ?? "", @event ?? "");
        var match = matches.FirstOrDefault(s => string.Equals(s.Url, targetUrl, StringComparison.OrdinalIgnoreCase));

        return Ok(new ExistsResponse(match is not null, match?.Id.ToString()));
    }

    /// <summary>
    /// Returns a single representative sample (the delivered envelope) so the node can
    /// show example data when the user pins/tests the trigger. Returns an array.
    /// </summary>
    [HttpGet("objects/{objectKey}/events/{eventKey}/samples")]
    public async Task<IActionResult> SamplesAsync(string objectKey, string eventKey)
    {
        if (await _catalog.GetEventAsync(Context, objectKey, eventKey) is null)
        {
            return NotFound(new { error = $"unknown object/event '{objectKey}/{eventKey}'" });
        }

        var sample = await _samples.CreateDeliveredSampleAsync(Context, objectKey, eventKey, Context.AccountId?.ToString() ?? "");
        return Ok(new[] { sample });
    }
}
