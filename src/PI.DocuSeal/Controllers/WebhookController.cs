using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using PI.DocuSeal.Models;
using PI.DocuSeal.Services;
using PI.Shared.Models;

namespace PI.DocuSeal.Controllers;

[ApiController]
[Route("docuseal/v1/[controller]")]
public class WebhookController(
    DocuSealWebhookService webhookService,
    IOptions<DocuSealWebhookConfiguration> config,
    ILogger<WebhookController> logger)
    : ControllerBase
{
    private readonly DocuSealWebhookConfiguration _config = config.Value;

    /// <summary>
    /// Handle DocuSeal webhook events
    /// </summary>
    /// <returns>Webhook processing result</returns>
    [HttpPost("{accountId}")]
    public async Task<IActionResult> HandleWebhook([FromRoute] Guid accountId)
    {
        try
        {
            // Read the raw request body
            string payload;
            using (var reader = new StreamReader(Request.Body))
            {
                payload = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrEmpty(payload))
            {
                logger.LogWarning("Received empty webhook payload");
                return BadRequest(new { error = "Empty payload" });
            }

            logger.LogInformation("Received DocuSeal webhook payload: {Payload}", payload);

            // Verify webhook signature if required
            if (_config.RequireSignatureVerification)
            {
                var signature = Request.Headers["X-DocuSeal-Signature"].FirstOrDefault();

                if (string.IsNullOrEmpty(signature))
                {
                    logger.LogWarning("Missing webhook signature header");
                    return Unauthorized(new { error = "Missing signature" });
                }

                if (!webhookService.VerifyWebhookSignature(payload, signature, _config.WebhookSecret))
                {
                    logger.LogWarning("Invalid webhook signature");
                    return Unauthorized(new { error = "Invalid signature" });
                }
            }

            // Parse webhook event
            var webhookEvent = BsonDocument.Parse(payload);
            if (webhookEvent == null)
            {
                logger.LogWarning("Webhook event is null after deserialization");
                return BadRequest(new { error = "Invalid webhook data" });
            }
            
            var processed = await webhookService.ProcessWebhookEventAsync(new AccountContext(accountId), webhookEvent);
            if (!processed)
            {
                logger.LogError("Failed to process webhook event: {Body}", webhookEvent);
                return StatusCode(500, new { error = "Failed to process webhook event" });
            }
            
            logger.LogInformation("Successfully processed DocuSeal webhook");

            return Ok(new
            {
                message = "Webhook processed successfully",
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing DocuSeal webhook");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}