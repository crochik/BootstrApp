namespace PI.Shared.Integrations.Delivery;

/// <summary>Lifecycle of a single delivery (one event × one subscription).</summary>
public enum DeliveryStatus
{
    /// <summary>Created, not yet attempted.</summary>
    Pending = 0,

    /// <summary>Claimed by a worker for an in-flight attempt.</summary>
    Delivering = 1,

    /// <summary>Succeeded (2xx).</summary>
    Delivered = 2,

    /// <summary>Failed but eligible for another attempt; waiting for its scheduled time.</summary>
    Retrying = 3,

    /// <summary>Permanent failure (non-retryable response such as 400/401/404).</summary>
    Failed = 4,

    /// <summary>Retries exhausted or the retry window elapsed.</summary>
    Dead = 5,
}

/// <summary>Result of a single HTTP delivery attempt.</summary>
public enum DeliveryOutcome
{
    /// <summary>2xx — success.</summary>
    Delivered,

    /// <summary>Transient failure (timeout, network error, 408/429/5xx) — retry later.</summary>
    RetryableFailure,

    /// <summary>Non-retryable response (e.g. 400/401/403/404/410/422) — give up.</summary>
    PermanentFailure,
}

/// <summary>Detailed result of an attempt, recorded in the delivery history.</summary>
public readonly record struct DeliveryResult(DeliveryOutcome Outcome, int? StatusCode, string Error, long DurationMs);
