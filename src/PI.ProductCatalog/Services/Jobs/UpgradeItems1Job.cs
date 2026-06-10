using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.ProductCatalog
{
    public class UpgradeItems1Job : IRunJob
    {
        private readonly ILogger<UpgradeItems1Job> _logger;
        private readonly MongoConnection _connection;

        public UpgradeItems1Job(ILogger<UpgradeItems1Job> logger, MongoConnection connection)
        {
            this._logger = logger;
            this._connection = connection;
        }

        public string Name => "ProductCatalogUpgradeItems";

        public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
        {
            var entities = await _connection.Filter<Entity>()
                .In("_t", new[] { nameof(Account), nameof(Organization) })
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.IsActive, true)
                .FindAsync();

            var modifiedCount = 0;
            foreach (var entity in entities)
            {
                _logger.LogInformation("Upgrade {Entity} {EntityId}", entity.Name, entity.Id);

                var shawCatalog = await _connection.Filter<CatalogFeed, B2BCatalogFeed>()
                    .Eq(x => x.AccountId, entity.AccountId)
                    .Eq(x => x.EntityId, entity.Id)
                    .Eq(x => x.SenderId, "045840055")
                    .Eq(x => x.IsActive, true)
                    .FirstOrDefaultAsync();

                using var scope = _logger.AddScope(new
                {
                    EntityId = entity.Id,
                    CatalogFeedId = shawCatalog?.Id,
                    Entity = entity.Name,
                    CatalogFeed = shawCatalog?.Name,
                });

                if (shawCatalog == null)
                {
                    _logger.LogInformation("No Shaw Catalog found");
                    continue;
                }

                using var cursor = _connection.Filter<CatalogItem>()
                    .Eq(x => x.AccountId, entity.AccountId)
                    .Eq(x => x.EntityId, entity.Id)
                    .Eq(x => x.CatalogFeedId, shawCatalog.Id)
                    .Ne("_upgrade_", 1)
                    // .Exists(x => x.KeyDates, false)
                    // .OrBuilder(
                    //     q => q.Exists(x => x.PendingDate),
                    //     q => q.Exists(x => x.EffectiveDate),
                    //     q => q.Exists(x => x.DroppedDate),
                    //     q => q.Exists(x => x.PromotionalStart),
                    //     q => q.Exists(x => x.PromotionalEnd),
                    //     q => q.Exists("Costs.DroppedDate"),
                    //     q => q.Exists("Costs.PromotionalStart"),
                    //     q => q.Exists("Costs.PromotionalEnd")
                    // )
                    .ToCursor();

                var count = 0;
                while (await cursor.MoveNextAsync())
                {
                    var batch = new List<MongoDB.Driver.UpdateOneModel<CatalogItem>>();
                    foreach (var row in cursor.Current)
                    {
                        row.Name = $"{row.StyleName} {row.ColorName}";
                        row.Update();

                        var model = _connection.Filter<CatalogItem>()
                            .Eq(x => x.Id, row.Id)
                            .Update
                                .SetOrUnset(x => x.KeyDates, row.KeyDates)
                                .SetOrUnset(x => x.Costs, row.Costs)
                                .SetOrUnset(x => x.Name, row.Name)
                                .SetOrUnset(x => x.Description, row.Description)
                                .SetOrUnset(x => x.IsActive, row.IsActive)
                                .SetOrUnset(x => x.CutCost, row.CutCost)
                                .SetOrUnset(x => x.StandardCost, row.StandardCost)
                                .SetOrUnset(x => x.PalletCost, row.PalletCost)
                                .SetOrUnset(x => x.StandardCost, row.StandardCost)
                                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                                .Set("_upgrade_", 1)
                            .UpdateOneModel();

                        batch.Add(model);

                        if (batch.Count > 1000)
                        {
                            _logger.LogInformation("Save {count} from {start}", batch.Count, count);
                            count += batch.Count;
                            await _connection.BulkWriteAsync(batch);
                            batch.Clear();
                        }
                    }

                    if (batch.Count > 0)
                    {
                        count += batch.Count;
                        await _connection.BulkWriteAsync(batch);
                        batch.Clear();
                    }
                }

                if (count > 0)
                {
                    await _connection.Filter<CatalogFeed, B2BCatalogFeed>()
                        .Eq(x => x.AccountId, entity.AccountId)
                        .Eq(x => x.EntityId, entity.Id)
                        .Update
                            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                            .Set(x => x.LastActor, context.Actor())
                            .Unset(x => x.Salesforce.StartedOn)
                        .UpdateManyAsync();

                    _logger.LogInformation("Upgraded {EntityId}: {Count}", entity.Id, count);

                    modifiedCount++;
                }

                if (modifiedCount > 10)
                {
                    _logger.LogInformation("Take a break...");
                    break;
                }
            }

            return new JobResult
            {
                Message = $"{modifiedCount} of {entities.Count} had items to upgrade",
                Result = new Dictionary<string, object>
                {
                    {"Total", entities.Count},
                    {"Processed", entities.Count},
                    {"Modified", modifiedCount}
                }
            };
        }
    }
}
