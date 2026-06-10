using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Webhook.Publisher.Configuration;
using Webhook.Publisher.Storage;
using Xunit;

namespace Webhook.Publisher.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class MongoEventStoreTests : IAsyncLifetime
{
#pragma warning disable CS0618 // Testcontainers builder image set via WithImage
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7").Build();
#pragma warning restore CS0618
    private MongoWebhookEventStore _store = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
        var options = Options.Create(new WebhookPublisherOptions
        {
            Mongo = { ConnectionString = _mongo.GetConnectionString(), Database = "webhooks_test" },
        });
        _store = new MongoWebhookEventStore(new MongoClient(_mongo.GetConnectionString()), options);
        await _store.EnsureIndexesAsync();
    }

    public Task DisposeAsync() => _mongo.DisposeAsync().AsTask();

    private static WebhookEvent NewEvent(string id) => new()
    {
        Id = id,
        TenantId = "t1",
        EventName = "order.created",
        OccurredAt = DateTime.UtcNow,
        Payload = BsonDocument.Parse("{\"orderId\":1}"),
        CreatedAt = DateTime.UtcNow,
    };

    private static WebhookDelivery NewDelivery(string id, string eventId, DeliveryStatus status, DateTime? next, DateTime updatedAt) => new()
    {
        Id = id,
        EventId = eventId,
        TenantId = "t1",
        EventName = "order.created",
        Url = "https://x.example/h",
        Secret = "s",
        Status = status,
        NextAttemptAt = next,
        CreatedAt = updatedAt,
        UpdatedAt = updatedAt,
    };

    [Fact]
    public async Task Saves_and_reads_event_and_deliveries()
    {
        await _store.SaveEventAsync(NewEvent("e1"));
        await _store.CreateDeliveriesAsync(new[] { NewDelivery("d1", "e1", DeliveryStatus.Pending, DateTime.UtcNow, DateTime.UtcNow) });

        Assert.NotNull(await _store.GetEventAsync("e1"));
        Assert.Equal("e1", (await _store.GetDeliveryAsync("d1"))!.EventId);
    }

    [Fact]
    public async Task Claim_is_exclusive_then_blocks_duplicates()
    {
        await _store.CreateDeliveriesAsync(new[] { NewDelivery("d2", "e1", DeliveryStatus.Pending, DateTime.UtcNow, DateTime.UtcNow) });
        var now = DateTime.UtcNow;
        var stale = now.AddMinutes(-5);

        var first = await _store.TryMarkDeliveringAsync("d2", now, stale);
        var second = await _store.TryMarkDeliveringAsync("d2", now, stale);

        Assert.NotNull(first);
        Assert.Equal(DeliveryStatus.Delivering, first!.Status);
        Assert.Null(second); // already claimed, not yet stale
    }

    [Fact]
    public async Task Stale_delivering_claim_can_be_reclaimed()
    {
        var staleUpdatedAt = DateTime.UtcNow.AddMinutes(-30);
        await _store.CreateDeliveriesAsync(new[] { NewDelivery("d3", "e1", DeliveryStatus.Delivering, null, staleUpdatedAt) });

        var now = DateTime.UtcNow;
        var reclaimed = await _store.TryMarkDeliveringAsync("d3", now, now.AddMinutes(-5));

        Assert.NotNull(reclaimed);
    }

    [Fact]
    public async Task RecordAttempt_increments_count_and_sets_status()
    {
        await _store.CreateDeliveriesAsync(new[] { NewDelivery("d4", "e1", DeliveryStatus.Delivering, null, DateTime.UtcNow) });
        var next = DateTime.UtcNow.AddSeconds(10);

        await _store.RecordAttemptAsync("d4",
            new DeliveryAttempt { Number = 1, At = DateTime.UtcNow, Outcome = DeliveryOutcomeKind.RetryableFailure, StatusCode = 500 },
            DeliveryStatus.Retrying, next);

        var d = await _store.GetDeliveryAsync("d4");
        Assert.Equal(1, d!.AttemptCount);
        Assert.Equal(DeliveryStatus.Retrying, d.Status);
        Assert.Single(d.Attempts);
        Assert.NotNull(d.FirstAttemptAt);
    }

    [Fact]
    public async Task Due_query_returns_overdue_and_stale_but_not_future()
    {
        var now = DateTime.UtcNow;
        await _store.CreateDeliveriesAsync(new[]
        {
            NewDelivery("due-pending", "e1", DeliveryStatus.Pending, now.AddMinutes(-10), now.AddMinutes(-10)),
            NewDelivery("future-retry", "e1", DeliveryStatus.Retrying, now.AddHours(1), now),
            NewDelivery("stale-delivering", "e1", DeliveryStatus.Delivering, null, now.AddMinutes(-30)),
        });

        var due = await _store.GetDueDeliveriesAsync(now.AddMinutes(-2), now.AddMinutes(-5), 100);
        var ids = due.Select(d => d.Id).ToHashSet();

        Assert.Contains("due-pending", ids);
        Assert.Contains("stale-delivering", ids);
        Assert.DoesNotContain("future-retry", ids);
    }
}
