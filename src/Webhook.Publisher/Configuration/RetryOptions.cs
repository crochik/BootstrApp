namespace Webhook.Publisher.Configuration;

/// <summary>
/// Exponential backoff schedule for failed deliveries. Each entry is the fixed
/// delay of one retry tier; there is one tiered delay queue per entry. After the
/// last tier — or once <see cref="MaxRetryWindow"/> has elapsed since the first
/// attempt — a delivery is marked <c>Dead</c>.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Backoff delays, one per retry tier. Default spans roughly a 24h window:
    /// 10s, 30s, 1m, 5m, 15m, 30m, 1h, 3h, 6h, 12h, 24h.
    /// </summary>
    public List<TimeSpan> Delays { get; set; } = new()
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
