namespace Webhook.Publisher.Configuration;

/// <summary>
/// Tuning for the delivery worker, the HTTP client and the outbox reconciler.
/// </summary>
public sealed class DeliveryOptions
{
    /// <summary>Per-consumer prefetch (unacked) limit. Bounds how many in-flight messages one consumer holds.</summary>
    public ushort PrefetchCount { get; set; } = 20;

    /// <summary>Number of concurrent consumers on the main delivery queue.</summary>
    public int ConsumerCount { get; set; } = 3;

    /// <summary>Per-request HTTP timeout for a single delivery attempt.</summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How often the outbox reconciler sweeps MongoDB for due deliveries that were not enqueued.</summary>
    public TimeSpan ReconcilerInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Grace period before the reconciler re-enqueues a due delivery, to avoid racing the normal path.</summary>
    public TimeSpan ReconcilerGracePeriod { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Max deliveries re-enqueued per reconciler sweep.</summary>
    public int ReconcilerBatchSize { get; set; } = 200;

    /// <summary>
    /// How long a delivery may stay <c>Delivering</c> before it is considered abandoned
    /// (worker crashed mid-attempt) and may be reclaimed/re-enqueued.
    /// </summary>
    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
