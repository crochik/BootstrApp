using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;
using Webhook.Publisher.Configuration;
using Webhook.Publisher.DependencyInjection;
using Webhook.Publisher.Publishing;
using Webhook.Publisher.Storage;
using Webhook.Publisher.Subscriptions;
using Xunit;

namespace Webhook.Publisher.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class RabbitMqEndToEndTests : IAsyncLifetime
{
#pragma warning disable CS0618 // Testcontainers builder image set via WithImage
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder().WithImage("rabbitmq:3.13-management").Build();
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7").Build();
#pragma warning restore CS0618

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_rabbit.StartAsync(), _mongo.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_rabbit.DisposeAsync().AsTask(), _mongo.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Publishes_and_delivers_a_signed_post()
    {
        using var sink = new TestSink(_ => HttpStatusCode.OK);
        var prefix = NewPrefix();
        await using var host = await StartHostAsync(prefix, "tenant-ok", sink.Url, new[] { TimeSpan.FromSeconds(1) });

        var publisher = host.Services.GetRequiredService<IWebhookPublisher>();
        var result = await publisher.PublishAsync("tenant-ok", "order.created", new { orderId = 7 });
        Assert.Equal(1, result.DeliveriesEnqueued);

        var delivery = await PollDeliveryAsync(prefix, result.EventId, DeliveryStatus.Delivered);
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);

        var request = Assert.Single(sink.Requests);
        Assert.StartsWith("t=", request.Signature);
        Assert.Contains("\"orderId\":7", request.Body);
        Assert.Equal(result.EventId, request.WebhookId);
    }

    [Fact]
    public async Task Retries_then_succeeds()
    {
        var attempts = 0;
        // Fail the first two attempts (500), then succeed.
        using var sink = new TestSink(_ => Interlocked.Increment(ref attempts) <= 2 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
        var prefix = NewPrefix();
        await using var host = await StartHostAsync(prefix, "tenant-retry", sink.Url,
            new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1) });

        var publisher = host.Services.GetRequiredService<IWebhookPublisher>();
        var result = await publisher.PublishAsync("tenant-retry", "order.created", new { orderId = 9 });

        var delivery = await PollDeliveryAsync(prefix, result.EventId, DeliveryStatus.Delivered, TimeSpan.FromSeconds(30));
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.True(delivery.AttemptCount >= 3, $"expected >=3 attempts, got {delivery.AttemptCount}");
    }

    [Fact]
    public async Task Exhausts_retries_and_parks_as_dead()
    {
        var attempts = 0;
        using var sink = new TestSink(_ => { Interlocked.Increment(ref attempts); return HttpStatusCode.InternalServerError; });
        var prefix = NewPrefix();
        // 2 tiers → 1 initial + 2 retries = 3 attempts, then Dead.
        await using var host = await StartHostAsync(prefix, "tenant-dead", sink.Url,
            new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1) });

        var publisher = host.Services.GetRequiredService<IWebhookPublisher>();
        var result = await publisher.PublishAsync("tenant-dead", "order.created", new { orderId = 13 });

        var delivery = await PollDeliveryAsync(prefix, result.EventId, DeliveryStatus.Dead, TimeSpan.FromSeconds(30));
        Assert.Equal(DeliveryStatus.Dead, delivery.Status);
        Assert.Equal(3, delivery.AttemptCount);

        // No hot-loop: give it a moment and confirm the sink is not still being hammered.
        var countAfterDead = attempts;
        await Task.Delay(TimeSpan.FromSeconds(3));
        Assert.Equal(countAfterDead, attempts);
    }

    // --- helpers ---

    private static string NewPrefix() => "wh" + Guid.NewGuid().ToString("n")[..8];

    /// <summary>Async-disposable wrapper so tests can <c>await using</c> the host (IHost is not IAsyncDisposable, and the connection manager is async-only-disposable).</summary>
    private sealed class HostScope : IAsyncDisposable
    {
        private readonly IHost _host;
        public IServiceProvider Services => _host.Services;
        public HostScope(IHost host) => _host = host;

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync();
            await ((IAsyncDisposable)_host.Services).DisposeAsync();
        }
    }

    private async Task<HostScope> StartHostAsync(string prefix, string tenant, string sinkUrl, IReadOnlyList<TimeSpan> delays)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);

        builder.Services.AddWebhookDeliveryWorker(builder.Configuration);
        builder.Services.RemoveAll<IWebhookSubscriptionStore>();
        builder.Services.AddSingleton<IWebhookSubscriptionStore>(new InMemoryWebhookSubscriptionStore(new[]
        {
            new WebhookSubscription { Id = "s1", TenantId = tenant, Url = sinkUrl, Secret = "shh", Enabled = true, Events = { "*" } },
        }));

        builder.Services.Configure<WebhookPublisherOptions>(o =>
        {
            o.RabbitMq.Uri = _rabbit.GetConnectionString();
            o.RabbitMq.ExchangePrefix = prefix;
            o.Mongo.ConnectionString = _mongo.GetConnectionString();
            o.Mongo.Database = "e2e";
            o.Mongo.DeliveriesCollection = prefix + "_deliveries";
            o.Mongo.EventsCollection = prefix + "_events";
            o.Retry.Delays = delays.ToList();
            o.Retry.MaxRetryWindow = TimeSpan.FromHours(1);
            o.Delivery.ConsumerCount = 1;
            o.Delivery.ReconcilerInterval = TimeSpan.FromSeconds(2);
            o.Delivery.HttpTimeout = TimeSpan.FromSeconds(5);
        });

        var host = builder.Build();
        await host.StartAsync();
        return new HostScope(host);
    }

    private async Task<WebhookDelivery> PollDeliveryAsync(string prefix, string eventId, DeliveryStatus target, TimeSpan? timeout = null)
    {
        var collection = new MongoClient(_mongo.GetConnectionString())
            .GetDatabase("e2e")
            .GetCollection<WebhookDelivery>(prefix + "_deliveries");

        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        WebhookDelivery? last = null;
        while (DateTime.UtcNow < deadline)
        {
            last = await collection.Find(d => d.EventId == eventId).FirstOrDefaultAsync();
            if (last is not null && last.Status == target)
            {
                return last;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Delivery for event {eventId} did not reach {target}; last status {last?.Status.ToString() ?? "<none>"}");
    }

    private sealed class TestSink : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Func<HttpListenerRequest, HttpStatusCode> _responder;
        private readonly CancellationTokenSource _cts = new();

        public ConcurrentQueue<Received> Requests { get; } = new();
        public string Url { get; }

        public TestSink(Func<HttpListenerRequest, HttpStatusCode> responder)
        {
            _responder = responder;
            var port = GetFreePort();
            Url = $"http://127.0.0.1:{port}/hook";
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _ = Task.Run(LoopAsync);
        }

        private async Task LoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync();
                }
                catch
                {
                    return;
                }

                using var reader = new StreamReader(ctx.Request.InputStream);
                var body = await reader.ReadToEndAsync();
                Requests.Enqueue(new Received(
                    ctx.Request.Headers["Webhook-Signature"] ?? "",
                    ctx.Request.Headers["Webhook-Id"] ?? "",
                    body));

                ctx.Response.StatusCode = (int)_responder(ctx.Request);
                ctx.Response.Close();
            }
        }

        private static int GetFreePort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Close();
            _cts.Dispose();
        }
    }

    private sealed record Received(string Signature, string WebhookId, string Body);
}
