using Webhook.Publisher.Configuration;

namespace Webhook.Publisher.Delivery;

/// <summary>What to do with a delivery after an attempt.</summary>
public enum DeliveryAction
{
    /// <summary>Mark Delivered.</summary>
    Complete,

    /// <summary>Schedule another attempt in the given tier.</summary>
    Retry,

    /// <summary>Permanent (non-retryable) failure — mark Failed.</summary>
    Fail,

    /// <summary>Retries exhausted or window elapsed — mark Dead.</summary>
    Dead,
}

/// <summary>A decision plus, for retries, the tier and delay to apply.</summary>
public readonly record struct DeliveryDecision(DeliveryAction Action, int Tier, TimeSpan Delay)
{
    public static DeliveryDecision Complete() => new(DeliveryAction.Complete, -1, TimeSpan.Zero);
    public static DeliveryDecision Fail() => new(DeliveryAction.Fail, -1, TimeSpan.Zero);
    public static DeliveryDecision Dead() => new(DeliveryAction.Dead, -1, TimeSpan.Zero);
    public static DeliveryDecision Retry(int tier, TimeSpan delay) => new(DeliveryAction.Retry, tier, delay);
}

/// <summary>
/// Pure decision logic mapping an attempt outcome + attempt count + elapsed time to
/// the next action. Kept free of any I/O so it is fully unit-testable.
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
        RetryOptions options)
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
                if (tier < 0 || tier >= options.Delays.Count)
                {
                    return DeliveryDecision.Dead();
                }

                if (now - firstAttemptAt >= options.MaxRetryWindow)
                {
                    return DeliveryDecision.Dead();
                }

                return DeliveryDecision.Retry(tier, options.Delays[tier]);
        }
    }
}
