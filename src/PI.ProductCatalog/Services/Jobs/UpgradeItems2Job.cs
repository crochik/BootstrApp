using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Mongo;
using Crochik.NET.APM;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.ProductCatalog
{
    /// <summary>
    /// Recalculate names and update all items for shaw/nourison
    /// </summary>
    public class UpgradeItems2Job : IRunJob
    {
        private readonly ILogger<UpgradeItems2Job> _logger;
        private readonly MongoConnection _connection;
        // private readonly IAPMService _apm;

        public UpgradeItems2Job(
            ILogger<UpgradeItems2Job> logger,
            MongoConnection connection)
        {
            this._logger = logger;
            this._connection = connection;
            // this._apm = apm;
        }

        public string Name => "ProductCatalogUpgradeItems";

        public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
        {
            // using var apm = _apm.StartTransaction("ExecuteJob", Name);

            var catalogFeeds = await _connection.Filter<CatalogFeed, B2BCatalogFeed>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.IsActive, true)
                .In(x => x.SenderId, new[] { Edi832.ShawSender.DefaultSenderId, Edi832.NourisonSender.DefaultSenderId })
                .FindAsync();

            var count = 0;
            foreach (var feed in catalogFeeds)
            {
                count += await UpdateItemsAsync(context, feed, stoppingToken);
            }

            // apm.Context = new
            // {
            //     context.AccountId,
            //     ModifiedFeeds = catalogFeeds.Count,
            //     ModifiedItems = count,
            // };
            //
            // _apm.SetResult("Success");

            return new JobResult
            {
                Message = $"{count} items for {catalogFeeds.Count} feeds were modified",
                Result = new Dictionary<string, object>
                {
                    {"Total", catalogFeeds.Count},
                    {"Processed", catalogFeeds.Count},
                    {"Modified", count}
                }
            };
        }

        private async Task<int> UpdateItemsAsync(IEntityContext context, B2BCatalogFeed feed, CancellationToken stoppingToken)
        {
            using var scope = _logger.BeginScope("Update {catalogFeed} {catalogFeedId}", feed.Description, feed.Id);

            var sender = feed.SenderId switch
            {
                Edi832.ShawSender.DefaultSenderId => (Edi832.AbstractCatalogFormat)new Edi832.ShawSender(),
                Edi832.NourisonSender.DefaultSenderId => new Edi832.NourisonSender(),
                _ => throw new NotImplementedException("sender not supported")
            };

            using var cursor = _connection.Filter<CatalogItem>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.CatalogFeedId, feed.Id)
                .ToCursor();

            _logger.LogInformation("Update {sender} items", sender.Name);

            var count = 0;
            while (await cursor.MoveNextAsync(stoppingToken))
            {
                var batch = new List<MongoDB.Driver.UpdateOneModel<CatalogItem>>();
                foreach (var row in cursor.Current)
                {
                    row.UpdateName(sender);
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
                        .UpdateOneModel();

                    batch.Add(model);

                    if (batch.Count > 500)
                    {
                        _logger.LogInformation("Write {page}", count);
                        count += batch.Count;
                        await _connection.BulkWriteAsync(batch);
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                {
                    _logger.LogInformation("Write {page}", count);
                    count += batch.Count;
                    await _connection.BulkWriteAsync(batch);
                    batch.Clear();
                }
            }

            await _connection.Filter<CatalogFeed, B2BCatalogFeed>()
                .Eq(x => x.Id, feed.Id)
                .Update
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, context.Actor())
                    .Unset(x => x.Salesforce.StartedOn)
                .UpdateOneAsync();

            _logger.LogInformation("{total} items modified", count);
            
            return count;
        }
    }
}
