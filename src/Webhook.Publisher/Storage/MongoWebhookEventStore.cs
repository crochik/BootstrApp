using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Webhook.Publisher.Configuration;

namespace Webhook.Publisher.Storage;

/// <summary>
/// MongoDB-backed <see cref="IWebhookEventStore"/>. Holds event payloads and the
/// authoritative per-delivery status/attempt history.
/// </summary>
public sealed class MongoWebhookEventStore : IWebhookEventStore
{
    private readonly IMongoCollection<WebhookEvent> _events;
    private readonly IMongoCollection<WebhookDelivery> _deliveries;

    public MongoWebhookEventStore(IMongoClient client, IOptions<WebhookPublisherOptions> options)
    {
        var mongo = options.Value.Mongo;
        var database = client.GetDatabase(mongo.Database);
        _events = database.GetCollection<WebhookEvent>(mongo.EventsCollection);
        _deliveries = database.GetCollection<WebhookDelivery>(mongo.DeliveriesCollection);
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var byEvent = new CreateIndexModel<WebhookDelivery>(
            Builders<WebhookDelivery>.IndexKeys.Ascending(d => d.EventId));

        // Supports the reconciler sweep: due deliveries by status + schedule.
        var byStatusDue = new CreateIndexModel<WebhookDelivery>(
            Builders<WebhookDelivery>.IndexKeys
                .Ascending(d => d.Status)
                .Ascending(d => d.NextAttemptAt));

        await _deliveries.Indexes.CreateManyAsync(new[] { byEvent, byStatusDue }, cancellationToken);
    }

    public Task SaveEventAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
        => _events.InsertOneAsync(webhookEvent, options: null, cancellationToken);

    public Task CreateDeliveriesAsync(IReadOnlyList<WebhookDelivery> deliveries, CancellationToken cancellationToken = default)
    {
        if (deliveries.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _deliveries.InsertManyAsync(deliveries, options: null, cancellationToken);
    }

    public Task<WebhookEvent?> GetEventAsync(string eventId, CancellationToken cancellationToken = default)
        => _events.Find(e => e.Id == eventId).FirstOrDefaultAsync(cancellationToken)!;

    public Task<WebhookDelivery?> GetDeliveryAsync(string deliveryId, CancellationToken cancellationToken = default)
        => _deliveries.Find(d => d.Id == deliveryId).FirstOrDefaultAsync(cancellationToken)!;

    public Task<WebhookDelivery?> TryMarkDeliveringAsync(string deliveryId, DateTime now, DateTime staleDeliveringBefore, CancellationToken cancellationToken = default)
    {
        var f = Builders<WebhookDelivery>.Filter;
        var claimable = f.Or(
            f.In(d => d.Status, new[] { DeliveryStatus.Pending, DeliveryStatus.Retrying }),
            f.And(
                f.Eq(d => d.Status, DeliveryStatus.Delivering),
                f.Lte(d => d.UpdatedAt, staleDeliveringBefore)));

        var filter = f.And(f.Eq(d => d.Id, deliveryId), claimable);

        // Keep NextAttemptAt as a lease marker; UpdatedAt drives the stale-claim check.
        var update = Builders<WebhookDelivery>.Update
            .Set(d => d.Status, DeliveryStatus.Delivering)
            .Set(d => d.UpdatedAt, now);

        // Return the post-update document so the caller has the claimed snapshot.
        var opts = new FindOneAndUpdateOptions<WebhookDelivery>
        {
            ReturnDocument = ReturnDocument.After,
        };

        return _deliveries.FindOneAndUpdateAsync(filter, update, opts, cancellationToken)!;
    }

    public Task RecordAttemptAsync(string deliveryId, DeliveryAttempt attempt, DeliveryStatus newStatus, DateTime? nextAttemptAt, CancellationToken cancellationToken = default)
    {
        var updateBuilder = Builders<WebhookDelivery>.Update
            .Push(d => d.Attempts, attempt)
            .Inc(d => d.AttemptCount, 1)
            .Set(d => d.Status, newStatus)
            .Set(d => d.UpdatedAt, attempt.At);

        // The first attempt establishes the start of the retry window.
        if (attempt.Number == 1)
        {
            updateBuilder = updateBuilder.Set(d => d.FirstAttemptAt, attempt.At);
        }

        updateBuilder = nextAttemptAt is null
            ? updateBuilder.Unset(d => d.NextAttemptAt)
            : updateBuilder.Set(d => d.NextAttemptAt, nextAttemptAt);

        return _deliveries.UpdateOneAsync(d => d.Id == deliveryId, updateBuilder, options: null, cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookDelivery>> GetDueDeliveriesAsync(DateTime dueBefore, DateTime staleDeliveringBefore, int limit, CancellationToken cancellationToken = default)
    {
        var f = Builders<WebhookDelivery>.Filter;
        var filter = f.Or(
            f.And(
                f.In(d => d.Status, new[] { DeliveryStatus.Pending, DeliveryStatus.Retrying }),
                f.Lte(d => d.NextAttemptAt, dueBefore)),
            f.And(
                f.Eq(d => d.Status, DeliveryStatus.Delivering),
                f.Lte(d => d.UpdatedAt, staleDeliveringBefore)));

        return await _deliveries.Find(filter).Limit(limit).ToListAsync(cancellationToken);
    }
}
