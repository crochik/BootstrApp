using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.ProductCatalog.Edi832;
using PI.ProductCatalog.Models;
using PI.Shared.Exceptions;
using PI.Shared.FileTransferProviders;
using PI.Shared.Models;
using PI.Shared.Services;
using Renci.SshNet.Common;
using FileInfo = PI.ProductCatalog.Models.FileInfo;

namespace PI.ProductCatalog.Services;

public class CatalogService
{
    public class Config
    {
        public string LocalTempPath { get; set; }
        public string Bucket { get; set; }
    }

    private readonly ILogger<CatalogService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ObjectTypeService _objectTypeService;
    private readonly MongoConnection _connection;
    private readonly IFileStorageService _fileStorageService;
    private readonly JobStatusService _jobStatusService;
    private readonly IEnumerable<IFileTransferProvider> _transferClients;
    private readonly Config _config;

    private string LocalTempPath => _config.LocalTempPath;
    private string Bucket => _config.Bucket;

    public CatalogService(
        ILogger<CatalogService> logger,
        IConfiguration configuration,
        IServiceScopeFactory serviceScopeFactory,
        ObjectTypeService objectTypeService,
        MongoConnection connection,
        IFileStorageService fileStorageService,
        JobStatusService jobStatusService,
        IEnumerable<IFileTransferProvider> transferClients
    )
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _objectTypeService = objectTypeService;
        _connection = connection;
        _fileStorageService = fileStorageService;
        _jobStatusService = jobStatusService;
        _transferClients = transferClients;
        _config = configuration.GetSection(GetType().Name).Get<Config>();
    }

    // private async Task<Result<CatalogSyncJob>> LoadNextAsync(IEntityContext context, Guid id)
    // {
    //     var query = _connection.Filter<CatalogFeed, B2BCatalogFeed>()
    //         .Eq(x => x.AccountId, context.AccountId.Value)
    //         .Eq(x => x.Id, id);

    //     switch (context.Role)
    //     {
    //         case EntityRoleId.Root:
    //         case EntityRoleId.Account:
    //         case EntityRoleId.Admin:
    //             break;
    //         default:
    //             query.Eq(x => x.EntityId, context.GetOwnerEntityId());
    //             break;
    //     }

    //     var catalogFeed = await query.FirstOrDefaultAsync();
    //     if (catalogFeed == null) throw new NotFoundException(nameof(CatalogFeed), id);

    //     return await LoadNextAsync(context, catalogFeed);
    // }

    private async Task<Result<CatalogSyncJob>> LoadNextAsync(IEntityContext context, B2BCatalogFeed catalogFeed)
    {
        var nextSyncDate = DateTime.UtcNow.AddHours(12);
        var status = default(string);
        var job = await CreateJobAsync(context, catalogFeed);
        try
        {
            var localPath = await DownloadNextFileAsync(context, catalogFeed, job);
            if (!string.IsNullOrEmpty(localPath))
            {
                var targetPath = $"catalog/{DateTime.UtcNow.Year}/{DateTime.UtcNow.Month}/{DateTime.UtcNow.Day}/{DateTime.UtcNow.Ticks}_{job.Id}_{job.FileInfo.Filename}";
                job.Url = $"s3://{Bucket}/{targetPath}";

                // make copy to s3?
                await _fileStorageService.UploadAsync(
                    inputPath: localPath,
                    contentType: "text/plain",
                    bucket: Bucket,
                    path: targetPath
                );

                await ProcessFileAsync(context, catalogFeed, job, localPath);

                status = job.IsSuccess ? $"Loaded {job.ItemsCount} items" : $"Failed: {job.Error}";
                nextSyncDate = DateTime.UtcNow.AddHours(job.IsSuccess ? 24 : 12);

                // remove/rename ftp file
                // ...
            }
            else
            {
                status = "No new Files";
                job.Error = "No new files";
                job.EndedOn ??= DateTime.UtcNow;
            }

            return Result.Success(job);
        }
        catch (SocketException ex)
        {
            // Connection reset by peer (most likely SSH)
            status = $"Connection Error: {ex.Message}";
            nextSyncDate = DateTime.UtcNow.AddMinutes(5);
            job.Error = ex.Message;
            throw;
        }
        catch (SshAuthenticationException ex)
        {
            status = $"Authentication Error: {ex.Message}";
            job.Error = ex.Message;
            throw;
        }
        catch (Exception ex)
        {
            status = $"Error: {ex.Message}";
            job.Error = ex.Message;
            throw;
        }
        finally
        {
            var updatedFeed = await MarkJobCompletedAsync(context, job, nextSyncDate, status);

            // update status of local copy
            catalogFeed.LastSync = updatedFeed.LastSync;
            catalogFeed.CurrentSync = updatedFeed.CurrentSync;

            //if (path != null) File.Delete(path);
        }
    }

    /// <summary>
    /// Reset All Margins
    /// </summary>
    public async Task ResetAllMarginsAsync(Models.ProductCatalog catalog)
    {
        using var scope = _logger.AddScope(new
        {
            catalog.EntityId,
            CatalogId = catalog.Id,
        });

        _logger.LogInformation("Reset Margins");

        var result = await _connection.DipperAsync(
            "CascadeMargin",
            $"{catalog.AccountId:N}",
            new
            {
                OrganizationId = catalog.EntityId.AsSerializedId(),
            }
        );

        _logger.LogInformation("Mark Feeds as modified: {result}", result);

        var feeds = await _connection.Filter<CatalogFeed>()
            .Eq(x => x.AccountId, catalog.AccountId)
            .Eq(x => x.EntityId, catalog.EntityId)
            .FindAsync();

        var context = new OrganizationContext(catalog.EntityId, catalog.AccountId);

        foreach (var feed in feeds)
        {
            await SetLastUpdatedOnAsync(context, feed);
        }
    }

    /// <summary>
    /// Reset all breadcrumbs for entity (and fill margins if missing)
    /// </summary>
    public async Task ResetBreadcrumbsAsync(IEntityContext context, Guid entityId)
    {
        using var scope = _logger.AddScope(new
        {
            EntityId = entityId
        });

        _logger.LogInformation("Reset Breadcrumbs");

        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, entityId)
            .FirstOrDefaultAsync();

        if (entity == null) throw new NotFoundException(nameof(Organization), entityId);

        // remove existing breadcrumbs
        var delete = await _connection.Filter<Breadcrumb>()
            .Eq(x => x.AccountId, entity.AccountId)
            .Eq(x => x.EntityId, entity.Id)
            .DeleteAsync();

        _logger.LogInformation("{count} breadcrumbs deleted", delete);

        // clear parents from items
        var update = await _connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, entity.AccountId)
            .Eq(x => x.EntityId, entity.Id)
            .Update
            .Unset(x => x.ParentIds)
            .Unset("Parents")
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateManyAsync();

        _logger.LogInformation("Updated {updated} items", update.ModifiedCount);

        update = await _connection.Filter<CatalogFeed>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, entityId)
            .Update
            .Set(x => x.LastUpdatedOn, DateTime.UtcNow)
            .Unset(x => x.Breadcrumbs)
            .UpdateManyAsync();

        _logger.LogInformation("Updated {updated} feeds", update.ModifiedCount);
    }

    public async Task SetLastUpdatedOnAsync(IEntityContext context, CatalogFeed catalog)
    {
        var now = DateTime.UtcNow;
        using var scope = _logger.AddScope(new
        {
            context.AccountId,
            context.OrganizationId,
            context.EntityId,
            CatalogFeedId = catalog.Id,
            CatalogFeed = catalog.Name,
            LastUpdatedOn = now
        });

        _logger.LogInformation("Update LastModifiedOn");

        await _connection.Filter<CatalogFeed>()
            .Eq(x => x.AccountId, catalog.AccountId)
            .Eq(x => x.Id, catalog.Id)
            .Update
            .Set(x => x.LastUpdatedOn, now)
            .Set(x => x.LastModifiedOn, now)
            .UpdateOneAsync();

        await _objectTypeService.FireObjectUpdatedAsync(context, catalog, null);
    }

    private async Task ProcessFileAsync(IEntityContext context, B2BCatalogFeed catalogFeed, CatalogSyncJob job, string path)
    {
        using var scope = _serviceScopeFactory.CreateScope();

        var senders = scope.ServiceProvider.GetServices<ICatalogFormat>().ToDictionary(x => x.SenderId);

        if (!senders.TryGetValue(catalogFeed.SenderId, out var sender))
        {
            sender = new UnkownSender();
            await AppendToLogAsync(job, "Parse file using generic parser");
        }
        else
        {
            await AppendToLogAsync(job, "Parse file (known format)");
        }

        var parserContext = new CatalogParserContext(context, job, path, sender);
        var parser = scope.ServiceProvider.GetRequiredService<CatalogParser>();
        try
        {
            await parser.ParseAsync(parserContext);
        }
        finally
        {
            job.EndedOn = DateTime.UtcNow;
        }
    }

    public async Task<bool> SyncAsync(IEntityContext context, B2BCatalogFeed feed, IEntity entity = null)
    {
        if (entity == null)
        {
            entity = await _connection.Filter<Entity>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, feed.EntityId)
                .FirstOrDefaultAsync();
        }

        var modified = false;

        await _jobStatusService.FireSyncStartedAsync(context, feed, "B2B Sync Started", new
        {
            Entity = entity?.Name,
            feed.Name
        });

        var filesProcessed = 0;
        while (true)
        {
            var result = await LoadNextAsync(context, feed);
            var syncedFile = result.IsSuccess && result.Value.IsSuccess;
            if (!syncedFile) break;

            filesProcessed++;
            modified = true;
        }

        if (modified)
        {
            await SetLastUpdatedOnAsync(context, feed);
        }

        await _jobStatusService.FireSyncFinsihedAsync(context, feed, "B2B Sync Finished",
            new Dictionary<string, object>
            {
                { "Entity", entity?.Name },
                { "Name", feed.Name },
                { "Processed", filesProcessed },
                { "Modified", modified },
            }
        );

        return modified;
    }

    public async Task<MALCatalogFeed> GetOrCreateMALCatalogAsync(IEntityContext context)
    {
        var catalogFeed = await _connection.Filter<CatalogFeed, MALCatalogFeed>(context)
            .FirstOrDefaultAsync();

        if (catalogFeed != null) return catalogFeed;

        catalogFeed = await _objectTypeService.CreateObjectAsync<MALCatalogFeed>(context);
        catalogFeed.Name = "Mobile Accessories & Labor";
        catalogFeed.ExternalId = "a0e41000009CUdkAAG"; // MOB supplier

        catalogFeed = await _objectTypeService.InsertAsync(context, catalogFeed);

        return catalogFeed;
    }

    private async Task AppendToLogAsync(CatalogSyncJob job, params string[] message)
    {
        foreach (var msg in message) _logger.LogInformation(msg);

        await _connection.Filter<CatalogSyncJob>()
            .Eq(x => x.Id, job.Id)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .PushEach(x => x.Log, message)
            .UpdateOneAsync();
    }

    private async Task<B2BCatalogFeed> MarkJobCompletedAsync(IEntityContext context, CatalogSyncJob job, DateTime nextSyncDate, string status)
    {
        await _connection.Filter<CatalogSyncJob>()
            .Eq(x => x.Id, job.Id)
            .Update
            .Set(x => x.EndedOn, job.EndedOn)
            .Set(x => x.Error, job.Error)
            .Set(x => x.FileInfo, job.FileInfo)
            .Set(x => x.Interchange, job.Interchange)
            .Set(x => x.LastModifiedOn, job.EndedOn)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.ItemsCount, job.ItemsCount)
            .Set(x => x.Url, job.Url)
            .Push(x => x.Log, job.IsSuccess ? "Finished Successfully" : "Failed")
            .UpdateOneAsync();

        var update = _connection.Filter<B2BCatalogFeed>()
                .Eq(x => x.Id, job.CatalogFeedId)
                .Eq(x => x.CurrentSync.JobId, job.Id)
                .Update
                .Set(x => x.LastModifiedOn, job.EndedOn)
                .Set(x => x.LastActor, context.Actor())
                .Unset(x => x.CurrentSync)
                // last sync                    
                .Set(x => x.LastSync.JobId, job.Id)
                .Set(x => x.LastSync.StartedOn, job.CreatedOn)
                .Set(x => x.LastSync.EndedOn, job.EndedOn)
                .Set(x => x.LastSync.Status, status)
                .Set(x => x.NextSyncDate, nextSyncDate)
            ;

        if (job.IsSuccess)
        {
            update
                .Set(x => x.ReceiverId, job.Interchange.ReceiverId)
                .Set(x => x.Version, job.Interchange.Version)
                .Set(x => x.IsTest, job.Interchange.IsTest)
                .Set(x => x.GroupControlNumber, job.Interchange.GroupControlNumber)
                .Set(x => x.GroupSenderCode, job.Interchange.GroupSenderCode)
                .Set(x => x.GroupReceiverCode, job.Interchange.GroupReceiverCode)
                // last sync                    
                .Set(x => x.LastSync.FileInfo, job.FileInfo)
                .Set(x => x.LastSync.InterchangeDate, job.Interchange?.InterchangeDate.GetValueOrDefault(DateTime.UtcNow))
                .Set(x => x.LastSync.TransactionControlNumber, job.Interchange?.TransactionControlNumber)
                ;
        }

        return await update.UpdateAndGetOneAsync();
    }

    private async Task<string> DownloadNextFileAsync(IEntityContext context, B2BCatalogFeed feed, CatalogSyncJob job)
    {
        var client = _transferClients.FirstOrDefault(x => x.CanHandle(feed.Url));
        if (client == null) throw new Exception("Protocol not supported");

        var serverUrl = new UriBuilder
        {
            Scheme = feed.Url.Scheme,
            Host = feed.Url.Host,
            Port = feed.Url.Port,
        }.Uri;

        await AppendToLogAsync(job, $"Try to connect to {serverUrl}");

        var connection = await client.ConnectAsync(
            serverUrl,
            feed.UserName,
            feed.Password,
            log
        );

        var path = feed.Url.AbsolutePath.Replace('/', '\\');
        await AppendToLogAsync(job, $"Get listing from {path}");

        var files = await connection.GetListingAsync(path);

        await AppendToLogAsync(job,
            $"Found {files.Length} items.",
            feed.LastSync?.TransactionControlNumber.HasValue == true ? $"Look for next file after {feed.LastSync.TransactionControlNumber}" : "Get First file"
        );

        var nextFile = files
            .Where(x => x.Name.EndsWith(".832"))
            .OrderBy(x => x.Date)
            .FirstOrDefault(x => x.IsFile &&
                                 (feed.LastSync?.FileInfo == null || feed.LastSync.FileInfo.ModifiedDate < x.Date)
            );

        if (nextFile == null)
        {
            await AppendToLogAsync(job, "No new files found");
            return null;
        }

        await AppendToLogAsync(job, $"Download {nextFile.Path}");

        var localPath = Path.Combine(LocalTempPath, feed.Id.ToString());
        Directory.CreateDirectory(localPath);

        var fullPath = Path.Combine(localPath, nextFile.Name);

        if (File.Exists(fullPath))
        {
            await AppendToLogAsync(job, $"Skip Download, use existing file: {fullPath}");
        }
        else
        {
            var localFileStream = new FileStream(fullPath, FileMode.CreateNew);
            await connection.DownloadAsync(localFileStream, nextFile);
            localFileStream.Close();
        }

        await connection.DisconnectAsync();

        job.FileInfo = new FileInfo
        {
            Url = new UriBuilder(serverUrl) { Path = nextFile.Path }.Uri,
            ModifiedDate = nextFile.Date,
            Filename = nextFile.Name,
        };

        job.Interchange = new CatalogUpdate
        {
            Id = Model.NewObjectId(),
            AccountId = job.AccountId,
            EntityId = job.EntityId,
            LastActor = context.Actor(),
            CatalogFeedId = job.CatalogFeedId,
            SenderId = feed.SenderId,
            ReceiverId = feed.ReceiverId,
            // GroupControlNumber = 
            // GroupReceiverCode =
            // GroupSenderCode = 
            // TransactionControlNumber = int.Parse(nextFile.Name.Split('.')[0]),
        };

        feed.CurrentSync.FileInfo = job.FileInfo;

        await _connection.Filter<CatalogSyncJob>()
            .Eq(x => x.Id, job.Id)
            .Update
            .Set(x => x.Interchange, job.Interchange)
            .Set(x => x.FileInfo, job.FileInfo)
            .UpdateOneAsync();

        await _connection.Filter<B2BCatalogFeed>()
            .Eq(x => x.Id, feed.Id)
            .Update
            .Set(x => x.CurrentSync.FileInfo, feed.CurrentSync.FileInfo)
            .UpdateOneAsync();

        return fullPath;

        async Task log(string message)
        {
            await AppendToLogAsync(job, message);
        }
    }

    private async Task<CatalogSyncJob> CreateJobAsync(IEntityContext context, B2BCatalogFeed feed)
    {
        var job = new CatalogSyncJob
        {
            Id = Model.NewObjectId(),
            CatalogFeedId = feed.Id,
            AccountId = feed.AccountId,
            EntityId = feed.EntityId,
            Name = $"{feed.Name} {DateTime.UtcNow:yy-MM-dd HH:mm}",
        };

        feed.CurrentSync = new SyncStatus
        {
            JobId = job.Id,
            StartedOn = job.CreatedOn
        };

        using var session = await _connection.StartSessionAsync();
        session.StartTransaction();

        try
        {
            await _connection.InsertAsync(session, job);

            var update = await _connection.Filter<B2BCatalogFeed>()
                .Eq(x => x.Id, feed.Id)
                .OrBuilder(
                    q => q.Exists(x => x.CurrentSync, false),
                    q => q.Lt(x => x.CurrentSync.StartedOn, DateTime.UtcNow.AddDays(-1))
                )
                .Update
                .Set(x => x.CurrentSync, feed.CurrentSync)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
                .UpdateOneAsync();

            if (update.ModifiedCount != 1) throw new Exception("Failed to start sync");
        }
        catch (Exception)
        {
            await session.AbortTransactionAsync();
            throw;
        }

        await session.CommitTransactionAsync();

        return job;
    }
}