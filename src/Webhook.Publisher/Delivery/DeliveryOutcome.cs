namespace Webhook.Publisher.Delivery;

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

/// <summary>Detailed result of an attempt for recording in the delivery history.</summary>
public readonly record struct DeliveryResult(DeliveryOutcome Outcome, int? StatusCode, string? Error, long DurationMs);
