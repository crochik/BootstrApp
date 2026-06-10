using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Webhook.Publisher.Configuration;

namespace Webhook.Publisher.Messaging;

/// <summary>
/// Declares the "wait room" retry topology:
/// <list type="bullet">
/// <item>topic exchange <c>{p}.delivery</c> → queue <c>{p}.delivery.q</c> (pattern <c>{p}.#</c>);</item>
/// <item>direct exchange <c>{p}.retry</c> → one delay queue per backoff tier, each with a fixed
/// <c>x-message-ttl</c> and dead-lettering back to the delivery exchange (no routing-key override, so the
/// original <c>webhook.{tenant}.{event}</c> key is preserved on the way back).</item>
/// </list>
/// </summary>
public sealed class RabbitMqTopologyInitializer : IWebhookTopologyInitializer
{
    private readonly IWebhookConnectionManager _connectionManager;
    private readonly WebhookPublisherOptions _options;
    private readonly WebhookTopologyNames _names;
    private readonly ILogger<RabbitMqTopologyInitializer> _logger;

    public RabbitMqTopologyInitializer(
        IWebhookConnectionManager connectionManager,
        IOptions<WebhookPublisherOptions> options,
        WebhookTopologyNames names,
        ILogger<RabbitMqTopologyInitializer> logger)
    {
        _connectionManager = connectionManager;
        _options = options.Value;
        _names = names;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _connectionManager.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(_names.DeliveryExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(_names.RetryExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(_names.DeliveryQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(_names.DeliveryQueue, _names.DeliveryExchange, _names.DeliveryBindingPattern, cancellationToken: cancellationToken);
        // Catch retried messages dead-lettered back from the delay tiers (they carry a retry.{tier} key).
        await channel.QueueBindAsync(_names.DeliveryQueue, _names.DeliveryExchange, _names.RetryComebackPattern, cancellationToken: cancellationToken);

        var delays = _options.Retry.Delays;
        for (var tier = 0; tier < delays.Count; tier++)
        {
            var args = new Dictionary<string, object?>
            {
                ["x-message-ttl"] = (int)delays[tier].TotalMilliseconds,
                ["x-dead-letter-exchange"] = _names.DeliveryExchange,
                // No x-dead-letter-routing-key: on expiry the message keeps its retry.{tier}
                // key and is re-routed to the delivery queue via the retry.# binding above.
            };

            await channel.QueueDeclareAsync(_names.RetryQueue(tier), durable: true, exclusive: false, autoDelete: false, arguments: args, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(_names.RetryQueue(tier), _names.RetryExchange, _names.RetryRoutingKey(tier), cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "Declared webhook topology: delivery exchange {Delivery}, retry exchange {Retry}, {Tiers} delay tiers",
            _names.DeliveryExchange, _names.RetryExchange, delays.Count);
    }
}
