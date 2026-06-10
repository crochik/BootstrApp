namespace Webhook.Publisher.Storage;

/// <summary>
/// Lifecycle of a single delivery (one event × one subscription).
/// </summary>
public enum DeliveryStatus
{
    /// <summary>Created, not yet attempted.</summary>
    Pending = 0,

    /// <summary>Claimed by a worker for an in-flight attempt.</summary>
    Delivering = 1,

    /// <summary>Succeeded (2xx).</summary>
    Delivered = 2,

    /// <summary>Failed but eligible for another attempt; waiting in a delay tier.</summary>
    Retrying = 3,

    /// <summary>Permanent failure (non-retryable response such as 400/401/404).</summary>
    Failed = 4,

    /// <summary>Retries exhausted or the retry window elapsed.</summary>
    Dead = 5,
}
