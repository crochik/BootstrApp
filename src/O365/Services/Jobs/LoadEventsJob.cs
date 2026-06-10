using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class LoadEventsJob : IRunJob
{
    private readonly ILogger<LoadEventsJob> _logger;
    private readonly MongoConnection _connection;
    private readonly O365Service _o365Service;

    public string Name => "O365Events";

    public LoadEventsJob(
        ILogger<LoadEventsJob> logger,
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
        // // total users 
        // var count = _connection.Filter<Entity, User>()
        //     .Eq(x => x.AccountId, context.AccountId.Value)
        //     .Eq(x => x.IsActive, true)
        //     .CountDocumentsAsync();
        
        var users = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.IsActive, true)
            .ElemMatchBuilder(
                x => x.Integrations,
                q => q
                    .Eq(f => f.IntegrationId, IntegrationIds.Office365)
                    .Eq(f => f.IsActive, true)
                    .OfTypeBuilder<EntityIntegration, O365Integration>(
                        q => q.OrBuilder(
                            q => q.Eq(x => x.LastSyncedOn, null),
                            q => q.Lt(x => x.LastSyncedOn, DateTime.UtcNow.AddDays(-7))
                        )
                    )
            )
            .FindAsync();

        var errors = 0;
        foreach (var user in users)
        {
            using var scope = _logger.BeginScope("Reload Events for {userId}", user.Id);
            if (user.UserRoleId != nameof(EntityRoleId.Admin) && !user.OrganizationId.HasValue)
            {
                _logger.LogInformation("Skip, corporate user {userRole}", user.UserRoleId);
                continue;
            }
            if (user.FirstIdentity(ExternalProvider.Microsoft) == null)
            {
                _logger.LogInformation("Skip, missing microsoft account");
                continue;
            }

            var userContext = user.Context.WithActorFrom(context);

            try
            {
                await _o365Service.ReloadEventsAsync(userContext);
                await UpdateO365IntegrationAsync(userContext, DateTime.UtcNow);
                _logger.LogInformation("Events reloaded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload events for {userId} {email}", user.Id, user.Email);
                errors++;
            }
        }

        var msg = $"{users.Count} processed.";
        if (errors>0) msg += $" {errors} user(s) failed.";

        return new JobResult
        {
            Message = $"{users.Count} processed",
            Result = new Dictionary<string, object>
            {
                { "Processed", users.Count },
                { "Failed", errors }
            }
        };
    }
    
    /// <summary>
    /// Update last synced date for user's o365 integration 
    /// </summary>
    private Task<User> UpdateO365IntegrationAsync(IEntityContext context, DateTime syncDate)
        => _connection.UserQuery(context)
            .ElemMatchBuilder(
                x => x.Integrations,
                f => f.Eq(i => i.IntegrationId, IntegrationIds.Office365)
            )
            .Update
            .Set($"{nameof(Entity.Integrations)}.$.{nameof(O365Integration.LastSyncedOn)}", syncDate)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();    
}