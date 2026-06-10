using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog.Services;

public class MongoDataService : IDataService
{
    private readonly ILogger<MongoDataService> _logger;
    private readonly MongoConnection _connection;
    private readonly List<InsertOneModel<CatalogOperation>> _batch = new();
    private readonly List<WriteModel<CatalogItem>> _itemBatch = new();
    private readonly HashSet<string> _skusInBatch = new();

    public MongoDataService(ILogger<MongoDataService> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public async Task<List<CatalogItem>> GetItemsAsync(CatalogStyleOperation op, string styleNumber)
    {
        var existing = await _connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, op.AccountId)
            .Eq(x => x.CatalogFeedId, op.CatalogFeedId)
            .Eq(x => x.StyleNumber, styleNumber)
            .FindAsync();

        return existing;
    }

    public async Task<CatalogItem> GetItemAsync(CatalogUpdate update, string sku)
    {
        var existing = await _connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, update.AccountId)
            .Eq(x => x.EntityId, update.EntityId)
            .Eq(x => x.CatalogFeedId, update.CatalogFeedId)
            .Eq(x => x.SKU, sku)
            .FirstOrDefaultAsync();

        return existing;
    }

    public void Add(CatalogItem item)
    {
        if (!_skusInBatch.Add(item.SKU))
        {
            _logger.LogError("More than one {SKU} in the same batch", item.SKU);
            return;
        }
        
        _itemBatch.Add(new InsertOneModel<CatalogItem>(item));
    }
    
    public void Add(CatalogOperation op)
    {
        _batch.Add(new InsertOneModel<CatalogOperation>(op));
    }

    public void Update(CatalogItemOperation op, CatalogItem existing, PropertyUpdate[] updates)
    {
        if (!_skusInBatch.Add(existing.SKU))
        {
            _logger.LogError("More than one {SKU} in the same batch", existing.SKU);
            return;
        }
        
        var update = _connection.Filter<CatalogItem>()
            .Eq(x => x.Id, existing.Id)
            .Update
            .Set(x => x.LastModifiedOn, op.LastModifiedOn)
            .Set(x => x.LastActor, op.LastActor);

        foreach (var diff in updates)
        {
            update.SetOrUnset(diff.Name, diff.After);
        }

        // calculated properties
        var ro = typeof(CatalogItem).GetProperties().Where(x => x.CanRead && !x.CanWrite);
        foreach (var prop in ro)
        {
            // assumes that a ro property needs the bsonelement to be serialized 
            if (prop.GetCustomAttributes(typeof(BsonElementAttribute), true).FirstOrDefault() is BsonElementAttribute bsonElement)
            {
                var value = prop.GetValue(existing);
                update.Set(bsonElement.ElementName ?? prop.Name, value);
            }
        }

        _itemBatch.Add(update.UpdateOneModel());
    }

    public async Task AppendToLogAsync(Guid jobId, string[] message)
    {
        await _connection.Filter<CatalogSyncJob>()
            .Eq(x => x.Id, jobId)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .PushEach(x => x.Log, message)
            .UpdateOneAsync();
    }

    public async Task FlushAsync(bool force)
    {
        if (_batch.Count >= 100 || (force && _batch.Count > 0))
        {
            // await AppendToLog(context._syncJob, $"Saving after {_count} items");
            await _connection.BulkWriteAsync(_batch);
            _batch.Clear();
        }

        if (_itemBatch.Count >= 100 || (force && _itemBatch.Count > 0))
        {
            await _connection.BulkWriteAsync(_itemBatch);
            _itemBatch.Clear();
            _skusInBatch.Clear();
        }
    }
    
    private async Task<object> UpdateMarginAsync(string jobId)
    {
        var parameters = new
        {
            JobId = jobId,
        };

        return await _connection.DipperAsync("AfterCatalogSync", "fci", parameters);
    }
}