using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.ProductCatalog.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.ProductCatalog;

public class BreadcrumbsJob : IRunJob
{
    private readonly ILogger<BreadcrumbsJob> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly JobStatusService _jobStatusService;

    public string Name => "Breadcrumbs";

    public BreadcrumbsJob(
        ILogger<BreadcrumbsJob> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        JobStatusService jobStatusService
    )
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _jobStatusService = jobStatusService;
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var list = await _connection.DipperAggregateAsync<CatalogFeed>("CatalogFeed.BreadcrumbsQueue", "productCatalog");

        _logger.LogInformation("Found {count} feeds", list.Count);

        var start = DateTime.UtcNow;
        var processed = 0;
        var partial = false;
        foreach (var feed in list)
        {
            using var feedScope = _logger.AddScope(new
            {
                CatalogFeed = feed.Id,
                Entity = feed.EntityId,
                File = processed
            });

            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Cancellation was requested");
                break;
            }

            await UpdateBreadcrumbsAsync(context, feed);

            processed++;

            if ((DateTime.UtcNow - start).TotalMinutes > 10)
            {
                _logger.LogInformation("Stopping job after 10 minutes to cooldown");
                partial = true;
                break;
            }
        }

        return new JobResult
        {
            Message = $"{processed} of {list.Count} were recalculated",
            Result = new Dictionary<string, object>
            {
                {"Total", list.Count},
                {"Modified", processed},
                {"Processed", processed},
                {"Partial", partial}
            }
        };
    }
    public async Task UpdateBreadcrumbsAsync(IEntityContext context, CatalogFeed feed)
    {
        var start = DateTime.UtcNow;

        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, feed.EntityId)
            .FirstOrDefaultAsync();

        using var scope = _logger.AddScope(new
        {
            feed.Id,
            feed.EntityId,
            CatalogFeed = feed.Name,
        });

        feed = await _connection.Filter<CatalogFeed>()
            .Eq(x => x.Id, feed.Id)
            .OrBuilder(
                q => q.Exists(x => x.Breadcrumbs.StartedOn, false),
                q => q.Lt(x => x.Breadcrumbs.StartedOn, DateTime.UtcNow.AddHours(-4)),
                q => q.Ne(x => x.Breadcrumbs.EndedOn, null))
            .Update
            .Set(x => x.Breadcrumbs.StartedOn, DateTime.UtcNow)
            .Unset(x => x.Breadcrumbs.EndedOn)
            .UpdateAndGetOneAsync();

        if (feed == null)
        {
            _logger.LogInformation("Skipping feed as it is being procesed by other process");
            return;
        }

        await _jobStatusService.FireSyncStartedAsync(context, feed, "Recalculate Breadcrumbs", new
        {
            Entity = entity?.Name,
            feed.Name
        });

        try
        {
            feed = await AddBreadcrumbsAsync(context, feed);

            var elapsed = DateTime.UtcNow - start;
            await _jobStatusService.FireSyncFinsihedAsync(
                context,
                feed,
                $"Recalculated Breadcrumbs in {elapsed.TotalSeconds}s",
                new Dictionary<string, object>
                {
                    {"Entity", entity?.Name},
                    {"Name", feed.Name},
                    {"Elapsed",elapsed.TotalMilliseconds}
                }
            );
        }
        catch (Exception ex)
        {
            await _jobStatusService.FireSyncFailedAsync(context, feed, "Recalculate Breadcrumbs", new
            {
                Entity = entity?.Name,
                feed.Name,
            });

            _logger.LogError(ex, "Failed to update breadcrumbs");
            throw;
        }
    }

    /// <summary>
    /// Upsert breadcrumbs for a catalog using "stored procedures"
    /// </summary>
    private async Task<CatalogFeed> AddBreadcrumbsAsync(IEntityContext context, CatalogFeed feed)
    {
        var result = await _connection.DipperAsync(
            "AddBreadcrumbs",
            $"{feed.AccountId:N}",
            new
            {
                AccountId = context.AccountId.Value.AsSerializedId(),
                EntityId = feed.EntityId.AsSerializedId(),
                CatalogFeedId = feed.Id.AsSerializedId(),
            });

        var now = DateTime.UtcNow;
        feed = await _connection.Filter<CatalogFeed>()
            .Eq(x => x.Id, feed.Id)
            .Update
            .Set(x => x.Breadcrumbs.EndedOn, now)
            .UpdateAndGetOneAsync();

        await FlagAsModifiedAsync(context, feed);
        return feed;
    }

    private async Task FlagAsModifiedAsync(IEntityContext context, CatalogFeed catalog)
    {
        using var scope = _logger.AddScope(new
        {
            context.AccountId,
            context.OrganizationId,
            context.EntityId,
            CatalogFeedId = catalog.Id,
        });

        _logger.LogInformation("Flag Catalog as modified");

        await _connection.Filter<CatalogFeed>()
            .Eq(x => x.AccountId, catalog.AccountId)
            .Eq(x => x.Id, catalog.Id)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateOneAsync();

        await _objectTypeService.FireObjectUpdatedAsync(context, catalog, null);
    }
}