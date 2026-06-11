using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Integrations.Catalog;
using PI.Shared.Integrations.Subscriptions;

namespace Zapier.Controllers;

/// <summary>
/// REST Hook endpoints. Zapier subscribes when a user turns a Zap on and unsubscribes
/// when they turn it off. The samples endpoint backs Zapier's "test trigger" step.
/// </summary>
[Authorize("zapier")]
[Route("/zapier/v1")]
public class SubscriptionController(
    ILogger<SubscriptionController> logger,
    IObjectCatalog catalog,
    ISubscriptionStore store,
    ISampleFactory samples
) : APIController
{
    /// <summary>Subscribe: registers Zapier's callback URL for an object/event.</summary>
    [HttpPost("subscriptions")]
    public async Task<IActionResult> SubscribeAsync([FromBody] SubscribeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TargetUrl) ||
            !Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return BadRequest(new { error = "targetUrl must be an absolute http(s) URL" });
        }

        if (await catalog.GetEventAsync(Context, request.Object ?? "", request.Event ?? "") is null)
        {
            return BadRequest(new { error = $"unknown object/event '{request.Object}/{request.Event}'" });
        }

        var subscription = await store.AddAsync(Context, request.Object, request.Event, request.TargetUrl);

        logger.LogInformation("Created Zapier subscription {SubscriptionId} for {ObjectType}/{Event}",
            subscription.Id, request.Object, request.Event);

        return Ok(new SubscribeResponse(subscription.Id.ToString(), request.Object, request.Event, subscription.Url));
    }

    /// <summary>Unsubscribe: removes a previously registered callback. Idempotent.</summary>
    [HttpDelete("subscriptions/{id}")]
    public async Task<IActionResult> UnsubscribeAsync(string id)
    {
        if (Guid.TryParse(id, out var subscriptionId))
        {
            await store.RemoveAsync(Context, subscriptionId);
        }

        return NoContent();
    }

    /// <summary>
    /// "Perform list": returns a single representative sample so Zapier can pull example
    /// data when the user tests the trigger. Zapier expects an array.
    /// </summary>
    [HttpGet("objects/{objectKey}/events/{eventKey}/samples")]
    public async Task<IActionResult> SamplesAsync(string objectKey, string eventKey)
    {
        if (await catalog.GetEventAsync(Context, objectKey, eventKey) is null)
        {
            return NotFound(new { error = $"unknown object/event '{objectKey}/{eventKey}'" });
        }

        var sample = await samples.CreateDeliveredSampleAsync(Context, objectKey, eventKey, Context.AccountId?.ToString() ?? "");
        return Ok(new[] { sample });
    }
}