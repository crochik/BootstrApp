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

namespace PI.ProductCatalog;

public class KeyDatesJob : IRunJob
{
    private readonly ILogger<KeyDatesJob> _logger;
    private readonly MongoConnection _connection;
    // private readonly IAPMService _apm;

    public KeyDatesJob(
        ILogger<KeyDatesJob> logger,
        MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public string Name => "ProductCatalogKeyDates";

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        // using var apm = _apm.StartTransaction("ExecuteJob", Name);

        var orgs = new HashSet<Guid>();

        using var cursor = _connection.Filter<CatalogItem>()
            .AnyLt(x => x.KeyDates, DateTime.UtcNow)
            .Eq(x => x.AccountId, context.AccountId.Value)
            .ToCursor();

        var count = 0;
        while (await cursor.MoveNextAsync(stoppingToken))
        {
            var batch = new List<MongoDB.Driver.UpdateOneModel<CatalogItem>>();
            foreach (var row in cursor.Current)
            {
                row.Update();

                var model = _connection.Filter<CatalogItem>()
                    .Eq(x => x.Id, row.Id)
                    .Update
                    .SetOrUnset(x => x.KeyDates, row.KeyDates)
                    .SetOrUnset(x => x.Costs, row.Costs)
                    .SetOrUnset(x => x.Description, row.Description)
                    .SetOrUnset(x => x.IsActive, row.IsActive)
                    .SetOrUnset(x => x.CutCost, row.CutCost)
                    .SetOrUnset(x => x.StandardCost, row.StandardCost)
                    .SetOrUnset(x => x.PalletCost, row.PalletCost)
                    .SetOrUnset(x => x.StandardCost, row.StandardCost)
                    // prices
                    .SetOrUnset(x => x.CutPrice, row.CutPrice)
                    .SetOrUnset(x => x.StandardPrice, row.StandardPrice)
                    .SetOrUnset(x => x.PalletPrice, row.PalletPrice)
                    .SetOrUnset(x => x.Prices, row.Prices)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .UpdateOneModel();

                batch.Add(model);

                if (batch.Count > 100)
                {
                    count += batch.Count;
                    await _connection.BulkWriteAsync(batch);
                    batch.Clear();
                }

                orgs.Add(row.EntityId);
            }

            if (batch.Count > 0)
            {
                count += batch.Count;
                await _connection.BulkWriteAsync(batch);
                batch.Clear();
            }
        }

        if (orgs.Count > 0)
        {
            await _connection.Filter<CatalogFeed, B2BCatalogFeed>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .In(x => x.EntityId, orgs)
                .Update
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
                .Unset(x => x.Salesforce.StartedOn)
                .UpdateManyAsync();
        }

        // apm.Context = new
        // {
        //     context.AccountId,
        //     ModifiedOrgs = orgs.Count,
        //     ModifiedItems = count,
        // };
        //
        // _apm.SetResult("Success");

        return new JobResult
        {
            Message = $"{count} items for {orgs.Count} were modified",
            Result = new Dictionary<string, object>
            {
                {"Total", orgs.Count},
                {"Processed", orgs.Count},
                {"Modified", count}
            }
        };
    }
}