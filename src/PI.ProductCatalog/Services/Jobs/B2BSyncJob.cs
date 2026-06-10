using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Edi832;
using PI.ProductCatalog.Models;
using PI.ProductCatalog.Services;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.ProductCatalog;

public class B2BSyncJob : IRunJob
{
    public string Name => "B2BSync";

    private readonly ILogger<B2BSyncJob> _logger;
    private readonly MongoConnection _connection;
    private readonly CatalogService _service;
    private readonly JobStatusService _jobStatusService;

    public B2BSyncJob(
        ILogger<B2BSyncJob> logger,
        MongoConnection connection,
        CatalogService service,
        JobStatusService jobStatusService
    )
    {
        _logger = logger;
        _connection = connection;
        _service = service;
        _jobStatusService = jobStatusService;
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var catalogFeedId = Environment.GetEnvironmentVariable("PI_CATALOGFEED");

        if (!string.IsNullOrWhiteSpace(catalogFeedId))
        {
            if (Guid.TryParse(catalogFeedId, out var id))
            {
                return await ExecuteSingleAsync(context, id, stoppingToken);
            }

            var result = catalogFeedId switch
            {
                "Shaw" => await ProcessAllForSupplierAsync(context, ShawSender.DefaultSenderId, stoppingToken),
                _ => new JobResult
                {
                    Message = $"Error, invalid id: {catalogFeedId}",
                    Result = new Dictionary<string, object>()
                }
            };

            return result;
        }

        return await ProcessQueueAsync(context, stoppingToken);
    }

    private async Task<JobResult> ExecuteSingleAsync(IEntityContext context, Guid catalogFeedId, CancellationToken stoppingToken)
    {
        // var update = await _connection.Filter<CatalogOperation>()
        //     .Eq(x => x.Id, Guid.Parse("000000006277dcc1f07d9a86c2530ce8"))
        //     .FirstOrDefaultAsync();

        var feed = await _connection.Filter<CatalogFeed, B2BCatalogFeed>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, catalogFeedId)
            .Eq(x => x.IsActive, true)
            .Exists(x => x.Password)
            .Update
            .Unset(x => x.CurrentSync)
            .UpdateAndGetOneAsync();

        if (feed == null)
        {
            return new JobResult
            {
                Message = $"Error, failed to load: {catalogFeedId}",
                Result = new Dictionary<string, object>()
            };
        }

        using var feedScope = _logger.AddScope(new
        {
            CatalogFeedId = feed.Id,
            CatalogFeed = feed.Name,
        });

        _logger.LogInformation("Start sync {description}", feed.Description);

        var wasModified = await _service.SyncAsync(context, feed);

        // // hack to reprocess local file
        // var job = await ProcessLocalFileAsync(context, feed, @"C:\DEVELOPMENT\github\SchedOnl\PI.ProductCatalog\temp\00000000-600e-29f6-5667-c1d6cc8ce4c7\000000950.832");
        // var wasModified = false;

        return new JobResult
        {
            Message = $"{feed.Description} sync completed.",
            Result = new Dictionary<string, object>
            {
                { "Total", 1 },
                { "Processed", 1 },
                { "Modified", wasModified ? 1 : 0 }
            }
        };
    }

    // private async Task<CatalogSyncJob> ProcessLocalFileAsync(IEntityContext context, B2BCatalogFeed feed, string filename)
    // {
    //     var job = await _service.CreateJobAsync(context, feed);
    //
    //     job.FileInfo = new Models.FileInfo
    //     {
    //         // Url = new UriBuilder(serverUrl) { Path = nextFile.Path }.Uri,
    //         // ModifiedDate = nextFile.Date,
    //         Filename = filename,
    //     };
    //
    //     job.Interchange = new Models.CatalogUpdate
    //     {
    //         Id = Model.NewObjectId(),
    //         AccountId = job.AccountId,
    //         EntityId = job.EntityId,
    //         LastActor = context.Actor(),
    //         CatalogFeedId = job.CatalogFeedId,
    //         SenderId = feed.SenderId,
    //         ReceiverId = feed.ReceiverId,
    //         // GroupControlNumber = 
    //         // GroupReceiverCode =
    //         // GroupSenderCode = 
    //         // TransactionControlNumber = int.Parse(nextFile.Name.Split('.')[0]),
    //     };
    //
    //     feed.CurrentSync.FileInfo = job.FileInfo;
    //
    //     await _service.ProcessFileAsync(context, feed, job, filename);
    //
    //     return job;
    // }

    private async Task<JobResult> ProcessAllForSupplierAsync(IEntityContext context, string senderId, CancellationToken stoppingToken)
    {
        var feeds = await _connection.Filter<CatalogFeed, B2BCatalogFeed>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.IsActive, true)
            .Eq(x => x.SenderId, senderId)
            .Exists(x => x.Password)
            .Exists(x => x.CurrentSync, false)
            .SortAsc(x => x.Id)
            .FindAsync();

        return await ProcessFeedsAsync(context, feeds, stoppingToken, true);
    }

    private async Task<JobResult> ProcessQueueAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var feeds = await _connection.Filter<CatalogFeed, B2BCatalogFeed>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.IsActive, true)
            .Exists(x => x.Password)
            .AndBuilder(
                and => and.OrBuilder(
                    q => q.Exists(x => x.NextSyncDate, false),
                    q => q.Lt(x => x.NextSyncDate, DateTime.UtcNow)
                ),
                and => and.OrBuilder(
                    q => q.Exists(x => x.CurrentSync, false),
                    q => q.Lt(x => x.CurrentSync.StartedOn, DateTime.UtcNow.AddDays(-1))
                )
            )
            .SortAsc(x => x.NextSyncDate)
            .FindAsync();

        return await ProcessFeedsAsync(context, feeds, stoppingToken);
    }

    private async Task<JobResult> ProcessFeedsAsync(IEntityContext context, List<B2BCatalogFeed> feeds, CancellationToken stoppingToken, bool forceSync = false)
    {
        var entities = new Dictionary<Guid, Entity>();

        _logger.LogInformation("Starting sync of {Feeds}", feeds.Count);

        var modifiedCount = 0;
        var processed = 0;
        foreach (var feed in feeds)
        {
            using var feedScope = _logger.AddScope(new
            {
                CatalogFeed = feed.Id,
                Entity = feed.EntityId,
            });

            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Cancellation was requested");
                break;
            }

            if (!entities.TryGetValue(feed.EntityId, out var entity))
            {
                entity = await _connection.Filter<Entity>()
                    .Eq(x => x.AccountId, context.AccountId)
                    .Eq(x => x.Id, feed.EntityId)
                    .FirstOrDefaultAsync();

                if (entity == null)
                {
                    _logger.LogError("Entity not found, skip feed");
                    continue;
                }

                entities.TryAdd(entity.Id, entity);
            }

            _logger.LogInformation("Start sync {index} of {description} for {entityName}", processed, feed.Description, entity.Name);

            if (await Sync(context, feed, entity, forceSync))
            {
                modifiedCount++;
            }

            processed++;
        }

        return new JobResult
        {
            Message = $"{modifiedCount} of {feeds.Count} had new files",
            Result = new Dictionary<string, object>
            {
                { "Total", feeds.Count },
                { "Processed", processed },
                { "Modified", modifiedCount }
            }
        };
    }

    private async Task<bool> Sync(IEntityContext context, B2BCatalogFeed feed, Entity entity, bool forceSync = false)
    {
        using var scope = _logger.AddScope(new
        {
            CatalogFeedId = feed.Id,
            CatalogFeed = feed.Name,
            Entity = entity.Name,
            EntityId = entity.Id,
        });

        var modified = false;

        try
        {
            var query = _connection.Filter<CatalogFeed, B2BCatalogFeed>()
                    .Eq(x => x.AccountId, context.AccountId.Value)
                    .Eq(x => x.IsActive, true)
                    .Eq(x => x.Id, feed.Id)
                    .Exists(x => x.Password)
                ;

            if (!forceSync)
            {
                query.AndBuilder(
                    and => and.OrBuilder(
                        q => q.Exists(x => x.NextSyncDate, false),
                        q => q.Lt(x => x.NextSyncDate, DateTime.UtcNow)
                    ),
                    and => and.OrBuilder(
                        q => q.Exists(x => x.CurrentSync, false),
                        q => q.Lt(x => x.CurrentSync.StartedOn, DateTime.UtcNow.AddDays(-1))
                    )
                );
            }
            else
            {
                query.OrBuilder(
                    q => q.Exists(x => x.CurrentSync, false),
                    q => q.Lt(x => x.CurrentSync.StartedOn, DateTime.UtcNow.AddDays(-1))
                );
            }

            feed = await query.FirstOrDefaultAsync();

            if (feed == null)
            {
                _logger.LogInformation("No longer a candidate");
                return false;
            }

            modified |= await _service.SyncAsync(context, feed, entity);
            _logger.LogInformation("SYNC {entity}, {userName}@{url}: {result}", entity.Name, feed.UserName, feed.Url, "SUCCESS");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SYNC {entity}, {userName}@{url}: {result}", entity.Name, feed.UserName, feed.Url, ex.Message);

            await _jobStatusService.FireSyncFailedAsync(context, feed, ex.Message, new
            {
                Entity = entity.Name,
                feed.Name,
            });
        }

        return modified;
    }
}