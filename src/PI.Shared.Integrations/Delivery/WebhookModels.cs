using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace PI.Shared.Integrations.Delivery;

/// <summary>
/// A published event, stored once with its full payload. The message broker never
/// carries the payload — the worker loads it from here by <see cref="Id"/> at delivery
/// time. One event is created per (object event × RBAC-flattened payload) group.
/// </summary>
[BsonCollection("webhook.Event")]
public sealed class WebhookEvent
{
    [BsonId]
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public string ObjectType { get; set; }

    public string EventKey { get; set; }

    /// <summary>Composite name delivered to subscribers: <c>"{objectType}.{eventKey}"</c>.</summary>
    public string EventName { get; set; }

    /// <summary>The flattened object body delivered as <c>data</c>.</summary>
    public Dictionary<string, object> Payload { get; set; }

    public string SchemaVersion { get; set; } = "1";

    public DateTime OccurredAt { get; set; }

    public DateTime CreatedOn { get; set; }
}

/// <summary>
/// One delivery attempt-track for a single (event × subscription) pair. Source of truth
/// for status, attempt count and the next scheduled attempt; the broker only references
/// it by <see cref="Id"/>.
/// </summary>
[BsonCollection("webhook.Delivery")]
public sealed class WebhookDelivery
{
    [BsonId]
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid AccountId { get; set; }

    public string EventName { get; set; }

    public Guid SubscriptionId { get; set; }

    // Snapshot of the subscription at publish time so later edits don't affect in-flight deliveries.
    public string Url { get; set; }
    public string Secret { get; set; }
    public string SignatureHeader { get; set; } = "Webhook-Signature";

    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    /// <summary>Number of attempts already made (drives the retry tier).</summary>
    public int AttemptCount { get; set; }

    public List<DeliveryAttempt> Attempts { get; set; } = new();

    public DateTime? FirstAttemptAt { get; set; }

    /// <summary>When the next attempt is due (set while <see cref="Status"/> is <c>Retrying</c>/<c>Pending</c>).</summary>
    public DateTime? NextAttemptAt { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime UpdatedOn { get; set; }
}

/// <summary>A single delivery attempt, appended to a <see cref="WebhookDelivery"/>'s history.</summary>
public sealed class DeliveryAttempt
{
    /// <summary>1-based attempt number.</summary>
    public int Number { get; set; }

    public DateTime At { get; set; }

    /// <summary>HTTP status code returned, when the request completed.</summary>
    public int? StatusCode { get; set; }

    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public DeliveryOutcome Outcome { get; set; }

    /// <summary>Error detail for non-success attempts.</summary>
    public string Error { get; set; }

    /// <summary>Round-trip duration of the attempt in milliseconds.</summary>
    public long DurationMs { get; set; }
}
