using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Models;
using Services;
using Stripe;

namespace Controllers;

[AllowAnonymous]
[Produces("application/json")]
[Route("/stripe/v1/[controller]")]
public class WebhookController : APIController
{
    private readonly ILogger<WebhookController> _logger;
    private readonly StripeService _service;

    public WebhookController(ILogger<WebhookController> logger, StripeService service)
    {
        _logger = logger;
        _service = service;
    }

    [HttpPost("{id}")]
    public async Task<IActionResult> HandleAsync([FromRoute] Guid id)
    {
        var config = await _service.GetSyncConfigAsync(id);
        if (config == null)
        {
            return BadRequest("Invalid Url");
        }

        var context = new AccountContext(id);
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

        try
        {
            var stripeEvent = string.IsNullOrEmpty(config.EndpointSecret) ?
                EventUtility.ParseEvent(json, false) :
                EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], config.EndpointSecret, throwOnApiVersionMismatch: false);

            _logger.LogInformation(
                "Received {EventType}: {Id} on {CreatedOn}",
                stripeEvent.Type, stripeEvent.Id, stripeEvent.Created
            );

            switch (stripeEvent.Type)
            {
                case Events.ChargeCaptured:
                case Events.ChargeExpired:
                case Events.ChargeFailed:
                case Events.ChargePending:
                case Events.ChargeSucceeded:
                case Events.ChargeUpdated:
                    await _service.UpsertAsync(context, stripeEvent.Data.Object as Charge);
                    break;

                case Events.ChargeDisputeCreated:
                    break;

                case Events.CustomerCreated:
                case Events.CustomerDeleted:
                case Events.CustomerUpdated:
                    await _service.UpsertAsync(context, stripeEvent.Data.Object as Customer);
                    break;

                case Events.CustomerSourceCreated:
                case Events.CustomerSourceDeleted:
                case Events.CustomerSourceExpiring:
                case Events.CustomerSourceUpdated:
                    break;
            }

            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to process webhook");
            return BadRequest();
        }
    }
}