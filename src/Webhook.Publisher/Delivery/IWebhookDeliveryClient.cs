using Webhook.Publisher.Storage;

namespace Webhook.Publisher.Delivery;

/// <summary>
/// Performs the actual HTTP POST of a signed webhook body to a subscriber endpoint.
/// </summary>
public interface IWebhookDeliveryClient
{
    Task<DeliveryResult> DeliverAsync(WebhookDelivery delivery, WebhookEvent webhookEvent, CancellationToken cancellationToken = default);
}
