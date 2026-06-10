using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using Webhook.Publisher.Storage;

namespace Webhook.Publisher.Delivery;

/// <summary>
/// Builds the canonical JSON envelope that is delivered to subscribers and signed.
/// The envelope wraps the caller's payload (<c>data</c>) with event metadata so the
/// receiver gets a stable, self-describing shape.
/// </summary>
public static class WebhookPayload
{
    private static readonly JsonWriterSettings RelaxedJson = new() { OutputMode = JsonOutputMode.RelaxedExtendedJson };

    /// <summary>Serializes the signed/delivered body for an event.</summary>
    public static byte[] Build(WebhookEvent webhookEvent)
    {
        var envelope = new JsonObject
        {
            ["eventId"] = webhookEvent.Id,
            ["tenantId"] = webhookEvent.TenantId,
            ["eventName"] = webhookEvent.EventName,
            ["occurredAt"] = webhookEvent.OccurredAt.ToUniversalTime().ToString("O"),
            ["schemaVersion"] = webhookEvent.SchemaVersion,
            ["data"] = JsonNode.Parse(webhookEvent.Payload.ToJson(RelaxedJson)),
        };

        return Encoding.UTF8.GetBytes(envelope.ToJsonString());
    }
}
