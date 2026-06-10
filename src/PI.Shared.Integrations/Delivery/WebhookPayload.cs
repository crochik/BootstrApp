using System.Text;
using Newtonsoft.Json;

namespace PI.Shared.Integrations.Delivery;

/// <summary>
/// Builds the canonical JSON envelope that is delivered to subscribers and signed.
/// The envelope wraps the flattened object body (<c>data</c>) with event metadata so
/// the receiver gets a stable, self-describing shape — the same shape the catalog's
/// sample factory advertises.
/// </summary>
public static class WebhookPayload
{
    /// <summary>Serializes the signed/delivered body for an event.</summary>
    public static byte[] Build(WebhookEvent webhookEvent)
    {
        var envelope = new
        {
            eventId = webhookEvent.Id.ToString("n"),
            tenantId = webhookEvent.AccountId.ToString(),
            eventName = webhookEvent.EventName,
            occurredAt = webhookEvent.OccurredAt.ToUniversalTime().ToString("O"),
            schemaVersion = webhookEvent.SchemaVersion,
            data = webhookEvent.Payload ?? new Dictionary<string, object>(),
        };

        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope));
    }
}
