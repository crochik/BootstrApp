namespace Webhook.Publisher.Configuration;

/// <summary>
/// Connection and topology settings for the RabbitMQ broker that backs the
/// outbound webhook queue. All broker object names are derived from
/// <see cref="ExchangePrefix"/> so the publisher, the topology initializer and
/// the delivery worker always agree.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>Broker host. Ignored when <see cref="Uri"/> is set.</summary>
    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    /// <summary>When set (e.g. <c>amqp://user:pass@host:5672/vhost</c>) it takes precedence over the discrete fields.</summary>
    public string? Uri { get; set; }

    /// <summary>Prefix for every exchange/queue name, e.g. <c>webhook</c> → <c>webhook.delivery</c>.</summary>
    public string ExchangePrefix { get; set; } = "webhook";

    /// <summary>Number of pooled publisher-confirm channels shared across publishes.</summary>
    public int PublishChannelPoolSize { get; set; } = 8;

    public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan RequestedHeartbeat { get; set; } = TimeSpan.FromSeconds(30);
}
