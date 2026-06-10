using MongoDB.Bson.Serialization.Attributes;

namespace Webhook.Publisher.Storage;

/// <summary>
/// A single delivery attempt, appended to a <see cref="WebhookDelivery"/>'s history.
/// </summary>
public sealed class DeliveryAttempt
{
    /// <summary>1-based attempt number.</summary>
    public int Number { get; set; }

    public DateTime At { get; set; }

    /// <summary>HTTP status code returned, when the request completed.</summary>
    public int? StatusCode { get; set; }

    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public DeliveryOutcomeKind Outcome { get; set; }

    /// <summary>Error detail for non-success attempts (exception message or status reason).</summary>
    public string? Error { get; set; }

    /// <summary>Round-trip duration of the attempt in milliseconds.</summary>
    public long DurationMs { get; set; }
}

/// <summary>
/// Persisted form of a delivery outcome. Mirrors <c>Delivery.DeliveryOutcome</c> but
/// lives in the storage layer so documents do not depend on delivery internals.
/// </summary>
public enum DeliveryOutcomeKind
{
    Delivered = 0,
    RetryableFailure = 1,
    PermanentFailure = 2,
}
