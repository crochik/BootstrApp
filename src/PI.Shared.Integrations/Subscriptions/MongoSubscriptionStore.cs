using System.Security.Cryptography;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace PI.Shared.Integrations.Subscriptions;

/// <summary>
/// MongoDB-backed <see cref="ISubscriptionStore"/>. Each integration binds a concrete
/// <typeparamref name="T"/> (carrying its own <c>[BsonCollection]</c>) so Zapier and
/// n8n persist into separate collections while sharing this logic.
/// </summary>
public sealed class MongoSubscriptionStore<T> : ISubscriptionStore
    where T : IntegrationSubscription, new()
{
    private readonly MongoConnection _connection;
    private readonly ILogger<MongoSubscriptionStore<T>> _logger;

    public MongoSubscriptionStore(MongoConnection connection, ILogger<MongoSubscriptionStore<T>> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<IntegrationSubscription> AddAsync(IContextWithActor context, string objectKey, string eventKey, string targetUrl)
    {
        // Replace any subscription from the same caller to the same URL (re-activation).
        await _connection.Filter<T>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.UserId.Value)
            .Eq(x => x.Url, targetUrl)
            .DeleteAsync();

        var subscription = new T
        {
            Id = Guid.NewGuid(),
            AccountId = context.AccountId.Value,
            EntityId = context.UserId.Value,
            OrganizationId = context.OrganizationId,
            ProfileId = context.ProfileId.Value,
            ClientId = context.ClientId,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            Name = $"{objectKey}: {eventKey}",
            ObjectType = objectKey,
            Keys = new[] { eventKey },
            Url = targetUrl,
            Secret = "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)),
        };

        await _connection.InsertAsync(subscription);

        _logger.LogInformation("Created subscription {SubscriptionId} for {ObjectType}/{Event} to {Url}",
            subscription.Id, objectKey, eventKey, targetUrl);

        return subscription;
    }

    public async Task<bool> RemoveAsync(IEntityContext context, Guid id)
    {
        var removed = await _connection.Filter<T>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, id)
            .DeleteAsync();

        return removed > 0;
    }

    public async Task<IReadOnlyList<IntegrationSubscription>> ListAsync(IEntityContext context)
    {
        var found = await _connection.Filter<T>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.UserId.Value)
            .SortAsc(x => x.CreatedOn)
            .FindAsync();

        return found;
    }

    public async Task<IntegrationSubscription> GetAsync(IEntityContext context, Guid id)
    {
        return await _connection.Filter<T>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.UserId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<IntegrationSubscription>> FindAsync(IEntityContext context, string objectKey, string eventKey)
    {
        var found = await _connection.Filter<T>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.UserId.Value)
            .Eq(x => x.ObjectType, objectKey)
            .AnyEq(x => x.Keys, eventKey)
            .FindAsync();

        return found;
    }

    public async Task<IReadOnlyList<IntegrationSubscription>> FindForDeliveryAsync(Guid accountId, string objectKey, string eventKey, Guid objectEntityId)
    {
        var found = await _connection.Filter<T>()
            .Eq(x => x.AccountId, accountId)
            .Eq(x => x.ObjectType, objectKey)
            .In(x => x.OrganizationId, new[] { default(Guid?), objectEntityId })
            .AnyEq(x => x.Keys, eventKey)
            .FindAsync();

        return found;
    }
}
