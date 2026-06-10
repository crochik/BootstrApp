namespace PI.Shared.Integrations.Delivery;

/// <summary>What to do with a delivery after an attempt.</summary>
public enum DeliveryAction
{
    /// <summary>Mark Delivered.</summary>
    Complete,

    /// <summary>Schedule another attempt.</summary>
    Retry,

    /// <summary>Permanent (non-retryable) failure — mark Failed.</summary>
    Fail,

    /// <summary>Retries exhausted or window elapsed — mark Dead.</summary>
    Dead,
}

/// <summary>A decision plus, for retries, the delay to apply before the next attempt.</summary>
public readonly record struct DeliveryDecision(DeliveryAction Action, TimeSpan Delay)
{
    public static DeliveryDecision Complete() => new(DeliveryAction.Complete, TimeSpan.Zero);
    public static DeliveryDecision Fail() => new(DeliveryAction.Fail, TimeSpan.Zero);
    public static DeliveryDecision Dead() => new(DeliveryAction.Dead, TimeSpan.Zero);
    public static DeliveryDecision Retry(TimeSpan delay) => new(DeliveryAction.Retry, delay);
}

/// <summary>
/// Pure decision logic mapping an attempt outcome + attempt count + elapsed time to the
/// next action. Free of any I/O so it is fully unit-testable.
/// </summary>
public static class RetrySchedule
{
    /// <param name="attemptsMade">Total attempts made so far, including the one just completed (1-based).</param>
    /// <param name="firstAttemptAt">Timestamp of the first attempt.</param>
    /// <param name="now">Current time.</param>
    public static DeliveryDecision Decide(
        int attemptsMade,
        DateTime firstAttemptAt,
        DateTime now,
        DeliveryOutcome outcome,
        DeliveryOptions options)
    {
        switch (outcome)
        {
            case DeliveryOutcome.Delivered:
                return DeliveryDecision.Complete();

            case DeliveryOutcome.PermanentFailure:
                return DeliveryDecision.Fail();

            case DeliveryOutcome.RetryableFailure:
            default:
                var tier = attemptsMade - 1;
                if (tier < 0 || tier >= options.RetryDelays.Count)
                {
                    return DeliveryDecision.Dead();
                }

                if (now - firstAttemptAt >= options.MaxRetryWindow)
                {
                    return DeliveryDecision.Dead();
                }

                return DeliveryDecision.Retry(options.RetryDelays[tier]);
        }
    }
}
