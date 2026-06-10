using MongoDB.Bson.Serialization.Attributes;

namespace Webhook.Publisher.Storage;

/// <summary>
/// One delivery attempt-track for a single (event × subscription) pair. This is the
/// unit RabbitMQ references (by <see cref="Id"/>) and the source of truth for status,
/// attempt count and the next scheduled attempt.
/// </summary>
public sealed class WebhookDelivery
{
    /// <summary>Delivery id (also the Mongo <c>_id</c>); the only thing carried on the wire.</summary>
    [BsonId]
    public string Id { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string EventName { get; set; } = string.Empty;

    public string SubscriptionId { get; set; } = string.Empty;

    // Snapshot of the subscription at publish time so later edits don't affect in-flight deliveries.
    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string SignatureHeader { get; set; } = "Webhook-Signature";

    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    /// <summary>Number of attempts already made (drives the retry tier).</summary>
    public int AttemptCount { get; set; }

    public List<DeliveryAttempt> Attempts { get; set; } = new();

    public DateTime? FirstAttemptAt { get; set; }

    /// <summary>When the next attempt is due (set while <see cref="Status"/> is <c>Retrying</c>/<c>Pending</c>).</summary>
    public DateTime? NextAttemptAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
