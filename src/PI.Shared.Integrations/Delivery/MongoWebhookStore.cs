using Crochik.Mongo;
using MongoDB.Driver;

namespace PI.Shared.Integrations.Delivery;

/// <summary>
/// <see cref="IWebhookStore"/> on <c>Crochik.Mongo</c>. Holds event payloads and the
/// authoritative per-delivery status/attempt history in the <c>webhook.Event</c> and
/// <c>webhook.Delivery</c> collections.
/// </summary>
public sealed class MongoWebhookStore(MongoConnection connection) : IWebhookStore
{
    public async Task SaveEventAsync(WebhookEvent webhookEvent) => await connection.InsertAsync(webhookEvent);

    public async Task CreateDeliveriesAsync(IEnumerable<WebhookDelivery> deliveries)
        => await connection.InsertManyAsync(deliveries);

    public async Task<WebhookEvent> GetEventAsync(Guid eventId)
        => await connection.Filter<WebhookEvent>().Eq(x => x.Id, eventId).FirstOrDefaultAsync();

    public async Task<WebhookDelivery> TryClaimAsync(Guid deliveryId, DateTime now, DateTime staleDeliveringBefore)
    {
        var f = Builders<WebhookDelivery>.Filter;
        var claimablePending = f.In(d => d.Status, new[] { DeliveryStatus.Pending, DeliveryStatus.Retrying });
        var claimableStale = f.And(
            f.Eq(d => d.Status, DeliveryStatus.Delivering),
            f.Lte(d => d.UpdatedOn, staleDeliveringBefore));

        // _id == deliveryId AND (pending/retrying OR stale-delivering)
        return await connection.Filter<WebhookDelivery>()
            .Eq(x => x.Id, deliveryId)
            .Or(claimablePending, claimableStale)
            .Update
            .Set(x => x.Status, DeliveryStatus.Delivering)
            .Set(x => x.UpdatedOn, now)
            .UpdateAndGetOneAsync();
    }

    public async Task RecordAttemptAsync(Guid deliveryId, DeliveryAttempt attempt, DeliveryStatus newStatus, DateTime? nextAttemptAt)
    {
        var update = connection.Filter<WebhookDelivery>()
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

        await update.UpdateOneAsync();
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

        return await connection.Filter<WebhookDelivery>()
            .Combine(f.Or(dueScheduled, dueStale))
            .Limit(limit)
            .FindAsync();
    }
}
