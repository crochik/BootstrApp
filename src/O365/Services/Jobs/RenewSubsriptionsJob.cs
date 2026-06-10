using System;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class RenewSubsriptionsJob : IRunJob
{
    private readonly ILogger<RenewSubsriptionsJob> _logger;
    private readonly MongoConnection _connection;
    private readonly O365Service _o365Service;

    public string Name => "O365RenewSubscriptions";

    public RenewSubsriptionsJob(
        ILogger<RenewSubsriptionsJob> logger,
        MongoConnection connection,
        O365Service o365Service
    )
    {
        _logger = logger;
        _connection = connection;
        _o365Service = o365Service;
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var list = await _connection.Filter<O365Subscription>()
            .Lt(x => x.ExpiresOn, DateTime.UtcNow.AddHours(4))
            .FindAsync();

        var count = 0;
        var inactive = 0;
        foreach (var subscription in list)
        {
            var user = await _connection.Filter<Entity, User>()
                .Eq(x => x.Id, subscription.EntityId)
                .FirstOrDefaultAsync();

            if (user == null || !user.IsActive)
            {
                await DeleteAsync(subscription);
                inactive++;
                continue;
            }

            if (await RenewAsync(user, subscription))
            {
                count++;
            }
        }

        var subscribed = 0;
        var failed = 0;
        var users = await _connection.DipperAggregateAsync<User>("UsersToSubscribe", "o365", new
        {
            AccountId = context.AccountId.Value.AsSerializedId(),
        });

        foreach (var user in users)
        {
            if (user.UserRoleId != nameof(EntityRoleId.Admin) && !user.OrganizationId.HasValue)
            {
                _logger.LogInformation("{UserId} is not an admin or assigned to an organization", user.Id);
                failed++;
                continue;
            }

            try
            {
                var subscription = await _o365Service.SubscribeToEventsAsync(user.Context);
                _logger.LogInformation("Created {SubscriptionId} for {UserId}", subscription.Id, user.Id);
                subscribed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create subscription for {UserId}", user.Id);
                failed++;
            }
        }

        return new JobResult
        {
            Message = $"Renewed {count} of {list.Count} subscriptions. Created {subscribed} new subscriptions",
        };
    }

    private async Task<bool> RenewAsync(User user, O365Subscription subscription)
    {
        try
        {
            await _o365Service.RenewAsync(user.Context, subscription);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to renew {subscriptionId} for {userId}", subscription.Id, user.Id);

            await DeleteAsync(subscription);
        }

        return false;
    }

    private async Task DeleteAsync(O365Subscription subscription)
    {
        await _connection.Filter<O365Subscription>()
            .Eq(x => x.Id, subscription.Id)
            .DeleteOneAsync();
    }
}