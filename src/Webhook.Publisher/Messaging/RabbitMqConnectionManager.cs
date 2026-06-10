using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Webhook.Publisher.Configuration;

namespace Webhook.Publisher.Messaging;

/// <summary>
/// Default <see cref="IWebhookConnectionManager"/>. Holds one recovering connection
/// (lazily created behind a semaphore) and a bounded pool of publisher-confirm
/// channels that callers rent and return.
/// </summary>
public sealed class RabbitMqConnectionManager : IWebhookConnectionManager
{
    private static readonly CreateChannelOptions ConfirmChannelOptions =
        new(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConnectionManager> _logger;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly ConcurrentBag<IChannel> _channelPool = new();

    private IConnection? _connection;
    private bool _disposed;

    public RabbitMqConnectionManager(IOptions<WebhookPublisherOptions> options, ILogger<RabbitMqConnectionManager> logger)
    {
        _options = options.Value.RabbitMq;
        _logger = logger;
    }

    public async ValueTask<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            // Replace a closed connection if recovery has given up.
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var factory = CreateFactory();
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
            _logger.LogInformation("Opened RabbitMQ connection to {Endpoint}", _connection.Endpoint);
            return _connection;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async ValueTask<IChannel> RentPublishChannelAsync(CancellationToken cancellationToken = default)
    {
        while (_channelPool.TryTake(out var pooled))
        {
            if (pooled.IsOpen)
            {
                return pooled;
            }

            await pooled.DisposeAsync();
        }

        var connection = await GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(ConfirmChannelOptions, cancellationToken);
        channel.BasicReturnAsync += OnBasicReturnAsync;
        return channel;
    }

    public async ValueTask ReturnPublishChannelAsync(IChannel channel)
    {
        if (!_disposed && channel.IsOpen && _channelPool.Count < _options.PublishChannelPoolSize)
        {
            _channelPool.Add(channel);
            return;
        }

        await channel.DisposeAsync();
    }

    private ConnectionFactory CreateFactory()
    {
        var factory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = _options.NetworkRecoveryInterval,
            RequestedHeartbeat = _options.RequestedHeartbeat,
            ClientProvidedName = "webhook-publisher",
        };

        if (!string.IsNullOrWhiteSpace(_options.Uri))
        {
            factory.Uri = new Uri(_options.Uri);
        }
        else
        {
            factory.HostName = _options.HostName;
            factory.Port = _options.Port;
            factory.VirtualHost = _options.VirtualHost;
            factory.UserName = _options.UserName;
            factory.Password = _options.Password;
        }

        return factory;
    }

    private Task OnConnectionShutdownAsync(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning("RabbitMQ connection shut down: {Reason}", e.ReplyText);
        return Task.CompletedTask;
    }

    private Task OnBasicReturnAsync(object? sender, BasicReturnEventArgs e)
    {
        // mandatory=true: the broker could not route this message to any queue.
        _logger.LogWarning(
            "Unroutable webhook message returned (reply {Code} {Text}, routingKey {RoutingKey}, messageId {MessageId})",
            e.ReplyCode, e.ReplyText, e.RoutingKey, e.BasicProperties.MessageId);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        while (_channelPool.TryTake(out var channel))
        {
            await channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        _connectionGate.Dispose();
    }
}
