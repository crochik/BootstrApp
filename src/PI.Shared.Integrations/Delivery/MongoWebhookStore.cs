using Crochik.Mongo;
using MongoDB.Driver;

namespace PI.Shared.Integrations.Delivery;

/// <summary>
/// <see cref="IWebhookStore"/> on <c>Crochik.Mongo</c>. Holds event payloads and the
/// authoritative per-delivery status/attempt history in the <c>webhook.Event</c> and
/// <c>webhook.Delivery</c> collections.
/// </summary>
public sealed class MongoWebhookStore : IWebhookStore
{
    private readonly MongoConnection _connection;

    public MongoWebhookStore(MongoConnection connection)
    {
        _connection = connection;
    }

    public Task SaveEventAsync(WebhookEvent webhookEvent) => _connection.InsertAsync(webhookEvent);

    public Task CreateDeliveriesAsync(IReadOnlyList<WebhookDelivery> deliveries)
        => deliveries.Count == 0 ? Task.CompletedTask : _connection.InsertManyAsync(deliveries);

    public Task<WebhookEvent> GetEventAsync(Guid eventId)
        => _connection.Filter<WebhookEvent>().Eq(x => x.Id, eventId).FirstOrDefaultAsync();

    public Task<WebhookDelivery> TryClaimAsync(Guid deliveryId, DateTime now, DateTime staleDeliveringBefore)
    {
        var f = Builders<WebhookDelivery>.Filter;
        var claimablePending = f.In(d => d.Status, new[] { DeliveryStatus.Pending, DeliveryStatus.Retrying });
        var claimableStale = f.And(
            f.Eq(d => d.Status, DeliveryStatus.Delivering),
            f.Lte(d => d.UpdatedOn, staleDeliveringBefore));

        // _id == deliveryId AND (pending/retrying OR stale-delivering)
        return _connection.Filter<WebhookDelivery>()
            .Eq(x => x.Id, deliveryId)
            .Or(claimablePending, claimableStale)
            .Update
            .Set(x => x.Status, DeliveryStatus.Delivering)
            .Set(x => x.UpdatedOn, now)
            .UpdateAndGetOneAsync();
    }

    public Task RecordAttemptAsync(Guid deliveryId, DeliveryAttempt attempt, DeliveryStatus newStatus, DateTime? nextAttemptAt)
    {
        var update = _connection.Filter<WebhookDelivery>()
            .Eq(x => x.Id, deliveryId)
            .Update
            .Push(x => x.Attempts, attempt)
            .Inc(x => x.AttemptCount, 1)
            .Set(x => x.Status, newStatus)
            .Set(x => x.UpdatedOn, attempt.At);

        // The first attempt establishes the start of the retry window.
        if (attempt.Number == 1)
        {
            update.Set(x => x.FirstAttemptAt, attempt.At);
        }

        if (nextAttemptAt is null)
        {
            update.Unset(x => x.NextAttemptAt);
        }
        else
        {
            update.Set(x => x.NextAttemptAt, nextAttemptAt);
        }

        return update.UpdateOneAsync();
    }

    public async Task<IReadOnlyList<WebhookDelivery>> GetDueDeliveriesAsync(DateTime dueBefore, DateTime staleDeliveringBefore, int limit)
    {
        var f = Builders<WebhookDelivery>.Filter;
        var dueScheduled = f.And(
            f.In(d => d.Status, new[] { DeliveryStatus.Pending, DeliveryStatus.Retrying }),
            f.Lte(d => d.NextAttemptAt, dueBefore));
        var dueStale = f.And(
            f.Eq(d => d.Status, DeliveryStatus.Delivering),
            f.Lte(d => d.UpdatedOn, staleDeliveringBefore));

        return await _connection.Filter<WebhookDelivery>()
            .Combine(f.Or(dueScheduled, dueStale))
            .Limit(limit)
            .FindAsync();
    }
}
