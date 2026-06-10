using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Mongo;
using MongoDB.Driver;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class CalculateAvailabilityJob : IRunJob
{
    private readonly MongoConnection _connection;
    private readonly AppointmentSchedulerService _schedulerService;

    public string Name => "UserAvailability";

    public CalculateAvailabilityJob(
        MongoConnection connection,
        AppointmentSchedulerService schedulerService
    )
    {
        _connection = connection;
        _schedulerService = schedulerService;
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var orgs = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.IsActive, true)
            .Eq(x => x.TimeZoneId, null)
            .FindAsync();

        var missing = orgs.Select(x => x.Id).ToHashSet();
        var result = await _connection.BulkWriteAsync(createModels(), 50);

        return new JobResult
        {
            Message = $"Calculated availability for {result} users",
        };

        async IAsyncEnumerable<WriteModel<UserAvailability>> createModels()
        {
            await foreach (var record in calculate())
            {
                yield return _connection.Filter<UserAvailability>().InsertOneModel(record);
            }
        }

        async IAsyncEnumerable<UserAvailability> calculate()
        {
            // var users = await _connection.DipperAggregateAsync<User>("UsersToSubscribe", "o365", new
            // {
            //     AccountId = context.AccountId.Value.AsSerializedId(),
            // });

            await _connection.Filter<UserAvailability>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .DeleteAsync();

            var users = await _connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.IsActive, true)
                .In(x => x.UserRoleId, new[] { nameof(EntityRoleId.Manager), nameof(EntityRoleId.User) })
                .Exists(x => x.Availability[0])
                .FindAsync();

            var start = DateTime.UtcNow.Date;
            var end = start.AddDays(29);

            foreach (var user in users)
            {
                var availability = await _schedulerService.GetUserAvailabilityAsync(user, start, end);
                if (availability != null) yield return availability;

                if (user.TimeZoneId == null) continue;
                if (!user.OrganizationId.HasValue) continue;
                    
                if (missing.Remove(user.OrganizationId.Value))
                {
                    await _connection.Filter<Entity, Organization>()
                        .Eq(x => x.AccountId, context.AccountId.Value)
                        .Eq(x => x.Id, user.OrganizationId.Value)
                        .Update
                        .Set(x => x.TimeZoneId, user.TimeZoneId)
                        .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                        .Set(x => x.LastActor, context.Actor())
                        .UpdateOneAsync();
                }
            }
        }
    }
}