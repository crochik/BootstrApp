using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Mongo;
using ExcelDataReader;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using PI.Shared.Models;
using PI.Shared.Models.Files;
using PI.Shared.Services;

namespace PI.Files.Services.Jobs;

public class ImportBusinessPlanJob : IRunJob
{
    private static Guid FileCreatedStatusId = Guid.Parse("3973cda7-96a2-4c16-8a47-de330b63f3bf");
    private static Guid FileImportedStatusId = Guid.Parse("519e3bd0-6f95-4804-a390-fac8bc4a2aff");
    private static Guid ImportFailedStatusId = Guid.Parse("d412d07f-7360-497a-9d36-fc51122123f9");
    
    private const string SHEET_BUSINESSSPLAN = "#6  2025 Financials";
    private const string SHEET_KPI = "#4  2025 KPI Budget";
    private const int YEAR = 2025;

    private readonly ILogger<ImportBusinessPlanJob> _logger;
    private readonly MongoConnection _connection;
    private readonly RemoteFileService _remoteFileService;
    private readonly ObjectTypeService _objectTypeService;

    public string Name => "ImportBusinessPlan";

    public ImportBusinessPlanJob(
        ILogger<ImportBusinessPlanJob> logger,
        MongoConnection connection,
        RemoteFileService remoteFileService,
        ObjectTypeService objectTypeService
    )
    {
        _logger = logger;
        _connection = connection;
        _remoteFileService = remoteFileService;
        _objectTypeService = objectTypeService;
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var failed = 0;
        var skipped = 0;
        var success = 0;

        var list = await _connection.Filter<OrganizationWithBusinessPlanRemoteFileId>()
            .Eq(x => x.AccountId, context.AccountId)
            .Ne(x => x.IsActive, false)
            .Ne(x => x.BusinessPlanRemoteFileId, null)
            .FindAsync();

        foreach (var org in list)
        {
            var remoteFile = await _connection.Filter<RemoteFile>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, org.BusinessPlanRemoteFileId)
                .Ne(x => x.IsActive, false)
                .Eq(x => x.ObjectStatusId, FileCreatedStatusId)
                .FirstOrDefaultAsync();

            if (remoteFile == null)
            {
                _logger.LogInformation("Skip {RemoteFileId} for {OrganizationId}", org.BusinessPlanRemoteFileId, org.Id);
                skipped++;
                continue;
            }

            // disable all existing 
            var disable = await _connection.Filter<BusinessPlan>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.EntityId, org.Id)
                .Eq(x => x.IsActive, true)
                .Eq(x => x.Year, YEAR)
                .Update
                .Set(x => x.IsActive, false)
                .Set(x => x.LastActor, Actor.Current)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateManyAsync();

            _logger.LogInformation("BP: Disabled {Rows} for {OrganizationId}", disable.MatchedCount, org.Id);

            // disable all existing 
            disable = await _connection.Filter<KPI>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.EntityId, org.Id)
                .Eq(x => x.IsActive, true)
                .Eq(x => x.Year, YEAR)
                .Update
                .Set(x => x.IsActive, false)
                .Set(x => x.LastActor, Actor.Current)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateManyAsync();

            _logger.LogInformation("KPI: Disabled {Rows} for {OrganizationId}", disable.MatchedCount, org.Id);

            var result = await ParseAsync(context, org.Id, remoteFile, stoppingToken);
            if (result != null)
            {
                _logger.LogError("Failed to import {RemoteFileId} for {OrganizationId}: {Error}", remoteFile.Id, org.Id, result);
                failed++;

                await _objectTypeService.UpdateObjectStatusAsync(context, nameof(RemoteFile), remoteFile.Id, ImportFailedStatusId);
                
                // TODO: fire error event
                // ????
                continue;
            }

            _logger.LogInformation("Successfully imported {RemoteFileId} for {OrganizationId}", remoteFile.Id, org.Id);
            await _objectTypeService.UpdateObjectStatusAsync(context, nameof(RemoteFile), remoteFile.Id, FileImportedStatusId);

            // TODO: FIRE EVENT?
            // EventIds.OnStatusEntered
            // ...

            success++;
        }

        return new JobResult
        {
            Message = $"Imported {success} Business Plan File(s)",
            Result = new Dictionary<string, object>
            {
                { "Total", list.Count },
                { "Skipped", skipped },
                { "Errors", failed },
                { "Successes", success },
            }
        };
    }

    private async Task<string> ParseAsync(IEntityContext context, Guid entityId, RemoteFile remoteFile, CancellationToken stoppingToken)
    {
        var stream = await _remoteFileService.GetStreamAsync(context, remoteFile);
        try
        {
            if (!stream.CanSeek)
            {
                // hack
                var copyOfStream = new MemoryStream();
                await stream.CopyToAsync(copyOfStream, stoppingToken);
                stream.Close();

                stream = copyOfStream;
            }

            var reader = ExcelReaderFactory.CreateOpenXmlReader(stream);
            var error = await importKPIAsync(reader);
            if (error != null)
            {
                _logger.LogError("Failed to import business plan");
                return error;
            }

            error = await importBusinessPlanAsync(reader);
            if (error != null)
            {
                _logger.LogError("Failed to import business plan");
                return error;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file");
            return ex.Message;
        }
        finally
        {
            stream?.Close();
        }

        return null;

        async Task<string> importKPIAsync(IExcelDataReader reader)
        {
            if (!reader.FindSheet(SHEET_KPI))
            {
                _logger.LogError("Failed to find sheet");
                return "Failed to find Sheet";
            }

            reader.Skip(2);

            var rows = loadKPIPlanRows(reader, 3) // "BUDGET" 
                    .Concat(loadKPIPlanRows(reader, 24)) // "Key Performance Indicators"
                    .ToArray()
                ;

            await _connection.BulkWriteAsync(rows.Select(x => new InsertOneModel<KPI>(x)));

            return null;
        }

        IEnumerable<KPI> loadKPIPlanRows(IExcelDataReader reader, int rows)
        {
            if (!reader.Read()) yield break;

            var category = reader.GetString(0);

            var index = 0;
            while (reader.Read() && index < rows)
            {
                var name = reader.GetString(0);
                if (string.IsNullOrWhiteSpace(name))
                {
                    index++;
                    continue;
                }

                yield return new KPI
                {
                    Id = Guid.NewGuid(),
                    AccountId = context.AccountId.Value,
                    EntityId = entityId,
                    CreatedOn = DateTime.UtcNow,
                    LastActor = Actor.Current,
                    Year = YEAR,
                    Row = index++,
                    Category = category,
                    Name = name,
                    Values = getColumns(13, 3).ToArray(),
                    IsActive = true,
                    IsTotal = name.StartsWith("Total "),
                };
            }

            yield break;

            IEnumerable<decimal?> getColumns(int count, int start = 1)
            {
                for (var i = 0; i < count; i++)
                {
                    var type = reader.GetFieldType(i + start);
                    if (type == typeof(double))
                    {
                        yield return (decimal)reader.GetDouble(i + start);
                    }
                    else
                    {
                        yield return default;
                    }
                }
            }
        }

        async Task<string> importBusinessPlanAsync(IExcelDataReader reader)
        {
            if (!reader.FindSheet(SHEET_BUSINESSSPLAN))
            {
                _logger.LogError("Failed to find sheet");
                return "Failed to find Sheet";
            }

            reader.Skip(3);

            var rows = loadBusinessPlanRows(reader, 2) // "BUDGET" 
                    .Concat(loadBusinessPlanRows(reader, 4)) // "REVENUE"
                    .Concat(loadBusinessPlanRows(reader, 8)) // "COST OF GOODS SOLD"
                    .Concat(loadBusinessPlanRows(reader, 5)) // "VARIABLE COSTS"
                    .Concat(loadBusinessPlanRows(reader, 47)) // "OVERHEAD"
                    .ToArray()
                ;

            await _connection.BulkWriteAsync(rows.Select(x => new InsertOneModel<BusinessPlan>(x)));

            return null;
        }

        IEnumerable<BusinessPlan> loadBusinessPlanRows(IExcelDataReader reader, int rows)
        {
            if (!reader.Read()) yield break;

            var category = reader.GetString(0);

            var index = 0;
            while (reader.Read())
            {
                var name = reader.GetString(0);
                if (name.StartsWith("NEW COA Line ")) name = name["NEW COA Line ".Length..];

                yield return new BusinessPlan
                {
                    Id = Guid.NewGuid(),
                    AccountId = context.AccountId.Value,
                    EntityId = entityId,
                    CreatedOn = DateTime.UtcNow,
                    LastActor = Actor.Current,
                    Year = YEAR,
                    Row = index++,
                    Category = category,
                    Name = name,
                    Values = getColumns(14).ToArray(),
                    IsActive = true,
                    IsTotal = name.StartsWith("TOTAL "),
                };

                if (index == rows) yield break;
            }

            yield break;

            IEnumerable<decimal?> getColumns(int count, int start = 1)
            {
                for (var i = 0; i < count; i++)
                {
                    var type = reader.GetFieldType(i + start);
                    if (type == typeof(double))
                    {
                        yield return (decimal)reader.GetDouble(i + start);
                    }
                    else
                    {
                        yield return default;
                    }
                }
            }
        }
    }

    private class OrganizationWithBusinessPlanRemoteFileId : Organization
    {
        public Guid? BusinessPlanRemoteFileId { get; set; }
    }

    [BsonCollection("bi.KPI")]
    public class KPI : EntityOwnedModel
    {
        public int Year { get; set; }
        public int Row { get; set; }
        public string Category { get; set; }

        [BsonIgnore] public decimal?[] Values { get; set; }

        [BsonElement] public decimal? January => Get(0);
        [BsonElement] public decimal? February => Get(1);
        [BsonElement] public decimal? March => Get(2);
        [BsonElement] public decimal? April => Get(3);
        [BsonElement] public decimal? May => Get(4);
        [BsonElement] public decimal? June => Get(5);
        [BsonElement] public decimal? July => Get(6);
        [BsonElement] public decimal? August => Get(7);
        [BsonElement] public decimal? September => Get(8);
        [BsonElement] public decimal? October => Get(9);
        [BsonElement] public decimal? November => Get(10);
        [BsonElement] public decimal? December => Get(11);

        public bool IsActive { get; set; }
        public bool IsTotal { get; set; }

        private decimal? Get(int index) => Values?.Length > index ? Values[index] : default(decimal?);
    }

    [BsonCollection("bi.BusinessPlan")]
    public class BusinessPlan : EntityOwnedModel
    {
        public int Year { get; set; }
        public int Row { get; set; }
        public string Category { get; set; }

        [BsonIgnore] public decimal?[] Values { get; set; }

        [BsonElement] public decimal? January => Get(0);
        [BsonElement] public decimal? February => Get(1);
        [BsonElement] public decimal? March => Get(2);
        [BsonElement] public decimal? April => Get(3);
        [BsonElement] public decimal? May => Get(4);
        [BsonElement] public decimal? June => Get(5);
        [BsonElement] public decimal? July => Get(6);
        [BsonElement] public decimal? August => Get(7);
        [BsonElement] public decimal? September => Get(8);
        [BsonElement] public decimal? October => Get(9);
        [BsonElement] public decimal? November => Get(10);
        [BsonElement] public decimal? December => Get(11);

        public bool IsActive { get; set; }
        public bool IsTotal { get; set; }

        private decimal? Get(int index) => Values?.Length > index ? Values[index] : default(decimal?);
    }
}

public static class IExcelDataReaderExtensions
{
    public static bool FindSheet(this IExcelDataReader reader, string name)
    {
        reader.Reset();

        do
        {
            if (reader.Name == name)
            {
                return true;
            }
        } while (reader.NextResult());

        return false;
    }

    public static bool Skip(this IExcelDataReader reader, int rows)
    {
        var count = 0;
        while (reader.Read())
        {
            count++;
            if (count == rows) return true;
        }

        return false;
    }
}