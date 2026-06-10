using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Integrations.Catalog;
using PI.Shared.Integrations.Subscriptions;

namespace Webhooks.Controllers;

/// <summary>
/// Generic REST Hook subscription management. An application subscribes a callback URL to
/// an object/event and receives a signed POST whenever it fires; it can list, fetch and
/// delete its own subscriptions. The <c>secret</c> returned on create (and get-by-id) lets
/// the application verify the delivery signature.
/// </summary>
[Authorize("webhooks")]
[Route("/webhooks/v1/subscriptions")]
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

    /// <summary>Subscribe a callback URL to an object/event. Returns the subscription incl. its signing secret.</summary>
    [HttpPost]
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

        _logger.LogInformation("Created webhook subscription {SubscriptionId} for {ObjectType}/{Event}",
            subscription.Id, request.Object, request.Event);

        var dto = SubscriptionDto.From(subscription, includeSecret: true);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = dto.Id }, dto);
    }

    /// <summary>Lists the caller's subscriptions (without secrets).</summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync()
    {
        var subscriptions = await _store.ListAsync(Context);
        return Ok(subscriptions.Select(s => SubscriptionDto.From(s, includeSecret: false)));
    }

    /// <summary>Fetches one subscription, including its signing secret.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(string id)
    {
        if (!Guid.TryParse(id, out var subscriptionId))
        {
            return NotFound();
        }

        var subscription = await _store.GetAsync(Context, subscriptionId);
        if (subscription is null)
        {
            return NotFound();
        }

        return Ok(SubscriptionDto.From(subscription, includeSecret: true));
    }

    /// <summary>Removes a subscription (idempotent).</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        if (Guid.TryParse(id, out var subscriptionId))
        {
            await _store.RemoveAsync(Context, subscriptionId);
        }

        return NoContent();
    }

    /// <summary>Returns a representative delivered envelope for an object/event, for testing.</summary>
    [HttpGet("/webhooks/v1/objects/{objectKey}/events/{eventKey}/samples")]
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
