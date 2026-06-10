using Microsoft.Extensions.Hosting;
using Webhook.Publisher.Storage;

namespace Webhook.Publisher.Messaging;

/// <summary>
/// Runs the one-time startup initialization — RabbitMQ topology and MongoDB indexes —
/// before the delivery worker begins consuming. Registered ahead of the worker so
/// hosted-service start ordering guarantees the topology exists first.
/// </summary>
public sealed class WebhookTopologyHostedService : IHostedService
{
    private readonly IWebhookTopologyInitializer _topology;
    private readonly IWebhookEventStore _eventStore;

    public WebhookTopologyHostedService(IWebhookTopologyInitializer topology, IWebhookEventStore eventStore)
    {
        _topology = topology;
        _eventStore = eventStore;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _eventStore.EnsureIndexesAsync(cancellationToken);
        await _topology.InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
