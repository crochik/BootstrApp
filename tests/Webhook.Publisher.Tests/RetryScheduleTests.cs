using Webhook.Publisher.Configuration;
using Webhook.Publisher.Delivery;
using Xunit;

namespace Webhook.Publisher.Tests;

public class RetryScheduleTests
{
    private static RetryOptions Options() => new()
    {
        Delays = new() { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1) },
        MaxRetryWindow = TimeSpan.FromHours(24),
    };

    [Fact]
    public void Delivered_completes()
    {
        var d = RetrySchedule.Decide(1, DateTime.UtcNow, DateTime.UtcNow, DeliveryOutcome.Delivered, Options());
        Assert.Equal(DeliveryAction.Complete, d.Action);
    }

    [Fact]
    public void Permanent_failure_fails()
    {
        var d = RetrySchedule.Decide(1, DateTime.UtcNow, DateTime.UtcNow, DeliveryOutcome.PermanentFailure, Options());
        Assert.Equal(DeliveryAction.Fail, d.Action);
    }

    [Theory]
    [InlineData(1, 0, 10)]
    [InlineData(2, 1, 30)]
    [InlineData(3, 2, 60)]
    public void Retryable_schedules_correct_tier_and_delay(int attemptsMade, int expectedTier, int expectedSeconds)
    {
        var now = DateTime.UtcNow;
        var d = RetrySchedule.Decide(attemptsMade, now, now, DeliveryOutcome.RetryableFailure, Options());

        Assert.Equal(DeliveryAction.Retry, d.Action);
        Assert.Equal(expectedTier, d.Tier);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), d.Delay);
    }

    [Fact]
    public void Retryable_beyond_last_tier_is_dead()
    {
        var now = DateTime.UtcNow;
        var d = RetrySchedule.Decide(4, now, now, DeliveryOutcome.RetryableFailure, Options());
        Assert.Equal(DeliveryAction.Dead, d.Action);
    }

    [Fact]
    public void Retryable_past_window_is_dead_even_with_tiers_left()
    {
        var firstAttempt = DateTime.UtcNow.AddHours(-25);
        var d = RetrySchedule.Decide(1, firstAttempt, DateTime.UtcNow, DeliveryOutcome.RetryableFailure, Options());
        Assert.Equal(DeliveryAction.Dead, d.Action);
    }
}
