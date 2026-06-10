using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Webhook.Publisher.Storage;

/// <summary>
/// A published event, stored once with its full payload. RabbitMQ never carries
/// the payload — workers load it from here by <see cref="Id"/> at delivery time.
/// </summary>
public sealed class WebhookEvent
{
    /// <summary>Event id (also the Mongo <c>_id</c>); doubles as a consumer idempotency key.</summary>
    [BsonId]
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string EventName { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    /// <summary>The caller-supplied payload, stored verbatim as BSON.</summary>
    public BsonDocument Payload { get; set; } = new();

    public string SchemaVersion { get; set; } = "1";

    public DateTime CreatedAt { get; set; }
}
