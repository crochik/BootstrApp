using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public abstract class AbstractObjectImporter<TSrc, TDst> : IObjectImporter<TSrc, TDst>
    where TSrc : SalesforceCustomObject
    where TDst : IFlowObject
{
    public DateTime? Since { get; set; }

    public abstract string SourceObjectTypeName { get; }
    public abstract string CollectionName { get; }

    protected readonly ILogger<AbstractObjectImporter<TSrc, TDst>> _logger;
    protected readonly MongoConnection _connection;
    protected readonly ObjectTypeService _objectTypeService;
    protected readonly DateTime _startDate = DateTime.UtcNow;
    protected ObjectType ObjectType { get; private set; }
    protected List<WriteModel<TSrc>> SourceQueue { get; } = new();
    protected List<WriteModel<TDst>> LoadedQueue { get; } = new();

    protected AbstractObjectImporter(
        ILogger<AbstractObjectImporter<TSrc, TDst>> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService
    )
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    public async Task ImportAsync(IEntityContext context)
    {
        await ValidateAsync(context);

        var cursor = _connection.Filter<TSrc>(ObjectType.CollectionName, ObjectType.DatabaseName)
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.LoadedOn, null)
            .SortAsc(x => x.LastModifiedOn)
            .Limit(10)
            // .WithBatchSize(100)
            .ToCursor();

        while (await cursor.MoveNextAsync())
        {
            foreach (var row in cursor.Current)
            {
                try
                {
                    var loadedItem = await ImportAsync(context, row);
                    if (loadedItem != null) LoadedQueue.Add(loadedItem);

                    // flag record as loaded if the date hasn't changed 
                    var update = _connection.Filter<TSrc>()
                        .Eq(x => x.Id, row.Id)
                        .Eq(x => x.LastModifiedOn, row.LastModifiedOn)
                        .Update
                        .Set(x => x.LoadedOn, _startDate)
                        .UpdateOneModel();

                    SourceQueue.Add(update);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to import {ObjectType} {ExternalId}", SourceObjectTypeName, row.ExternalId);
                    continue;
                }

                if (SourceQueue.Count >= 200)
                {
                    await WritePageAsync();
                }
            }
        }

        await WritePageAsync();
    }

    public async Task<TDst> CreateAsync(IEntityContext context, TSrc sfObject)
    {
        using var scope = _logger.AddScope(new
        {
            sfObject.ObjectType,
            sfObject.Id,
            sfObject.ExternalId,
        });
        
        var model = await AddAsync(context, sfObject);
        if (model == null)
        {
            _logger.LogInformation("Can't add object because it is incomplete");
            return default(TDst);
        }
        
        _logger.LogInformation("Create");

        var update = (UpdateOneModel<TDst>)model;
        var result = await _connection.GetCollection<TDst>(CollectionName).UpdateOneAsync(update.Filter, update.Update, new UpdateOptions()
        {
            IsUpsert = update.IsUpsert,
        });

        // var result = await _connection.BulkWriteAsync(CollectionName, new[] { model });
        if (result.UpsertedId != null)
        {
            var bsonId = result.UpsertedId;
            var id = bsonId.IsString ? Guid.Parse(bsonId.AsString) : bsonId.AsObjectId.ToGuid();

            _logger.LogInformation("Inserted with {Id}", id);
            var row = await _connection.Filter<TDst>(CollectionName).Eq(x => x.Id, id).FirstOrDefaultAsync();
            if (row == null) throw NotFoundException.New<TDst>(id);

            await _objectTypeService.FireCreateEventAsync(context, row, e =>
            {
                e.Description = $"{SourceObjectTypeName} imported from Salesforce";
                e.Action ??= "ObjectCreated";
                e.AddRefValue(sfObject.ObjectType, sfObject.Id);
                e.AddRefValue(nameof(SalesforceCustomObject.ExternalId), sfObject.ExternalId);
            });

            return row;
        }

        if (result.ModifiedCount == 1)
        {
            var row = await GetAsync(context, sfObject);
            if (row == null) throw new NotFoundException("Couldn't find object that was updated");

            _logger.LogInformation("{ObjectId} modified", row.Id);

            await _objectTypeService.FireObjectUpdatedAsync(context, row, null, e =>
            {
                e.Description = $"{SourceObjectTypeName} imported from Salesforce";
                e.AddRefValue(sfObject.ObjectType, sfObject.Id);
                e.AddRefValue(nameof(SalesforceCustomObject.ExternalId), sfObject.ExternalId);
            });

            return row;
        }

        if (result.MatchedCount == 1)
        {
            _logger.LogInformation("Object was up to date");
            return default(TDst);
        }

        throw new Exception("Failed to upset object");
    }

    public virtual async Task<WriteModel<TDst>> ImportAsync(IEntityContext entityContext, TSrc row)
    {
        var dst = await GetAsync(entityContext, row);

        return dst == null ? await AddAsync(entityContext, row) : await UpdateAsync(entityContext, row, dst);
    }

    private async Task WritePageAsync()
    {
        // TODO: make into a transaction?
        // ...
        if (LoadedQueue.Count > 0)
        {
            var result = await _connection.BulkWriteAsync(CollectionName, LoadedQueue);
            _logger.LogInformation("{Object}: {Inserted} {Modified}", SourceObjectTypeName, result.InsertedCount, result.ModifiedCount);
        }

        if (SourceQueue.Count > 0)
        {
            var result2 = await _connection.BulkWriteAsync(ObjectType.CollectionName, SourceQueue, ObjectType.DatabaseName);
            _logger.LogInformation("{Count} processed", result2.ModifiedCount);
        }

        SourceQueue.Clear();
        LoadedQueue.Clear();
    }

    protected virtual async Task ValidateAsync(IEntityContext context)
    {
        ObjectType = await _objectTypeService.GetAsync(context, SourceObjectTypeName);
        if (ObjectType == null) throw new NotFoundException($"{SourceObjectTypeName} not found");
    }

    protected TParam GetRequired<TParam>(CustomObject src, string customProperty)
    {
        if (src.TryGetProperty(customProperty, out TParam obj))
        {
            return obj;
        }

        throw new NotFoundException($"Missing required property {customProperty}");
    }

    protected abstract Task<TDst> GetAsync(IEntityContext entityContext, TSrc row);
    protected abstract ValueTask<WriteModel<TDst>> UpdateAsync(IEntityContext context, TSrc src, TDst dst);
    protected abstract ValueTask<WriteModel<TDst>> AddAsync(IEntityContext context, TSrc src);
}

public abstract class AbstractObjectImporter<TDst> : AbstractObjectImporter<SalesforceCustomObject, TDst>
    where TDst : IFlowObject
{
    protected AbstractObjectImporter(ILogger<AbstractObjectImporter<TDst>> logger, MongoConnection connection, ObjectTypeService objectTypeService) :
        base(logger, connection, objectTypeService)
    {
    }
}