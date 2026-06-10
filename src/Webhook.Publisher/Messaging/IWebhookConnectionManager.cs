using RabbitMQ.Client;

namespace Webhook.Publisher.Messaging;

/// <summary>
/// Owns a single long-lived, auto-recovering RabbitMQ connection and a pool of
/// publisher-confirm channels. Replaces the anti-pattern of opening a connection
/// and channel per publish.
/// </summary>
public interface IWebhookConnectionManager : IAsyncDisposable
{
    /// <summary>Gets the shared connection, creating it on first use.</summary>
    ValueTask<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Rents a publisher-confirm channel from the pool (or creates one).</summary>
    ValueTask<IChannel> RentPublishChannelAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a channel to the pool, disposing it if faulted or the pool is full.</summary>
    ValueTask ReturnPublishChannelAsync(IChannel channel);
}
