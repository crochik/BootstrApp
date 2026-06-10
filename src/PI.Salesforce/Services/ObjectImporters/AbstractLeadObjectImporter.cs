using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public abstract class AbstractLeadObjectImporter : IObjectImporter
{
    public abstract Guid LeadTypeId { get; }
    public abstract string SourceObjectTypeName { get; }

    private readonly ILogger<AbstractLeadObjectImporter> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly LeadBuilderService _leadBuilderService;

    public ObjectType ObjectType { get; private set; }
    public IEntityContext Context { get; private set; }
    public LeadType LeadType { get; private set; }

    public AbstractLeadObjectImporter(
        ILogger<AbstractLeadObjectImporter> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        LeadBuilderService leadBuilderService
    )
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _leadBuilderService = leadBuilderService;
    }

    public async Task ImportAsync(IEntityContext context)
    {
        Context = context;

        ObjectType = await _objectTypeService.GetAsync(Context, SourceObjectTypeName);
        if (ObjectType == null) throw new NotFoundException($"{SourceObjectTypeName} not found");

        LeadType = await _connection.Filter<LeadType>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, LeadTypeId)
            .FirstOrDefaultAsync();

        if (LeadType == null) throw new NotFoundException($"Could not find {LeadTypeId}");

        var startDate = DateTime.UtcNow;
        var cursor = _connection.Filter<SalesforceCustomObject>(ObjectType.CollectionName, ObjectType.DatabaseName)
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.LoadedOn, null)
            .SortAsc(x => x.LastModifiedOn)
            // .Limit(10)
            .WithBatchSize(1000)
            .ToCursor();

        var loadedQueue = new List<WriteModel<SalesforceCustomObject>>();
        while (await cursor.MoveNextAsync())
        {
            foreach (var row in cursor.Current)
            {
                var result = await ImportLeadAsync(row);
                if (result.IsSuccess)
                {
                    // flag record as loaded if the date hasn't changed 
                    var update = _connection.Filter<SalesforceCustomObject>()
                        .Eq(x => x.Id, row.Id)
                        .Eq(x => x.LastModifiedOn, row.LastModifiedOn)
                        .Update
                        .Set(x => x.LoadedOn, startDate)
                        .UpdateOneModel();

                    loadedQueue.Add(update);

                    if (loadedQueue.Count >= 200) await writePageAsync();
                }
            }
        }

        await writePageAsync();

        async Task writePageAsync()
        {
            if (loadedQueue.Count < 1) return;

            var result2 = await _connection.BulkWriteAsync(loadedQueue);
            _logger.LogInformation("{Count} processed", result2.ModifiedCount);

            loadedQueue.Clear();
        }
    }

    private async Task<Result<Guid?>> ImportLeadAsync(SalesforceCustomObject row)
    {
        using var scope = _logger.BeginScope("{ObjectType} {ExternalId}", SourceObjectTypeName, row.ExternalId);

        var json = JsonConvert.SerializeObject(row.Properties, Formatting.None, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        var builder = await _leadBuilderService.AddAsync(Context, LeadType, json, fireEvents: false);

        if (builder.Failed)
        {
            _logger.LogError("Failed: {Error}", builder.Error);
            return Result.Error<Guid?>(builder.Error);
        }

        _logger.LogInformation("{Action}: {LeadId} for {EntityId}", builder.ExistingLead != null ? "Updated" : "Created", builder.LeadId, builder.EntityId);
        return Result.Success<Guid?>(builder.LeadId);
    }
}