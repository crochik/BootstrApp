using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Webhook.Publisher.Publishing;

namespace Webhook.Zapier.Tests;

/// <summary>
/// Boots the app for endpoint tests without MongoDB/RabbitMQ: the durable pipeline's
/// hosted services are removed and <see cref="IWebhookPublisher"/> is replaced with an
/// in-memory recorder, so publishing is observable without infrastructure.
/// </summary>
public sealed class ZapierAppFactory : WebApplicationFactory<Program>
{
    public RecordingWebhookPublisher Publisher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Topology init, delivery worker and reconciler would connect to RabbitMQ/Mongo.
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IWebhookPublisher>();
            services.AddSingleton<IWebhookPublisher>(Publisher);
        });
    }
}

/// <summary>Captures published events instead of enqueuing them.</summary>
public sealed class RecordingWebhookPublisher : IWebhookPublisher
{
    public List<(string Tenant, string EventName, object Payload)> Published { get; } = new();

    public Task<PublishResult> PublishAsync(string tenantId, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        Published.Add((tenantId, eventName, payload));
        return Task.FromResult(new PublishResult("evt_test", 1, true));
    }
}
