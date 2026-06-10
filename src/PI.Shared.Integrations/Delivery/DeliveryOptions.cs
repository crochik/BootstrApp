namespace PI.Shared.Integrations.Delivery;

/// <summary>
/// Tuning for the delivery worker, the HTTP client and the outbox reconciler. Bound
/// from the <c>WebhookDelivery</c> configuration section.
/// </summary>
public sealed class DeliveryOptions
{
    public const string SectionName = "WebhookDelivery";

    /// <summary>Topic the publisher emits delivery references on and the worker binds to.</summary>
    public string DeliveryTopic { get; set; } = "webhook.delivery";

    /// <summary>Per-request HTTP timeout for a single delivery attempt.</summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How often the outbox reconciler sweeps MongoDB for due deliveries.</summary>
    public TimeSpan ReconcilerInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Grace period before the reconciler re-enqueues a due delivery, to avoid racing the normal path.</summary>
    public TimeSpan ReconcilerGracePeriod { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Max deliveries re-enqueued per reconciler sweep.</summary>
    public int ReconcilerBatchSize { get; set; } = 200;

    /// <summary>
    /// How long a delivery may stay <c>Delivering</c> before it is considered abandoned
    /// (worker crashed mid-attempt) and may be reclaimed/re-enqueued.
    /// </summary>
    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Exponential backoff delays, one per retry tier. Defaults span ~24h:
    /// 10s, 30s, 1m, 5m, 15m, 30m, 1h, 3h, 6h, 12h, 24h. After the last tier — or once
    /// <see cref="MaxRetryWindow"/> elapses since the first attempt — a delivery is marked Dead.
    /// </summary>
    public List<TimeSpan> RetryDelays { get; set; } = new()
    {
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(3),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
        TimeSpan.FromHours(24),
    };

    /// <summary>Hard cap on total retry duration measured from the first attempt.</summary>
    public TimeSpan MaxRetryWindow { get; set; } = TimeSpan.FromHours(24);
}
