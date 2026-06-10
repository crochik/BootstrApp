using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Mongo;
using CsvHelper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Files;
using PI.Shared.Services;
using Result = PI.Shared.Models.Result;

namespace PI.Files.Services.Jobs;

public class ImportObjectsJob : IRunJob
{
    private readonly ILogger<ImportObjectsJob> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly RemoteFileService _remoteFileService;
    private readonly Configuration _configuration;

    private Shared.Models.ImportObjectsJob _job;
    private RemoteFile _remoteFile;
    private IEntityContext _context;
    private ObjectTypeWithImportOptions _objectType;
    private ISpreadsheetFileParser _parser;
    private Dictionary<string, int> _sourceColumns;
    private KeyValuePair<string, object>[] _staticColumns;
    private List<SourceToReferenceField> _sourceToReferenceFields;

    public string Name => "ImportObjects";

    public ImportObjectsJob(
        IConfiguration configuration,
        ILogger<ImportObjectsJob> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        RemoteFileService remoteFileService)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _remoteFileService = remoteFileService;
        _configuration = configuration.GetSection(nameof(ImportObjectsJob)).Get<Configuration>();
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        // allow to replay 
        var job = await _connection.Filter<Shared.Models.ImportObjectsJob>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, _configuration.ImportJobId)
            // .OrBuilder(
            //     q => q.Eq(x => x.StartedOn, null),
            //     q => q.Ne(x => x.EndedOn, null)
            // )
            .Update
            .Set(x => x.StartedOn, DateTime.UtcNow)
            .Unset(x => x.EndedOn)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        if (job == null) throw NotFoundException.New(nameof(Shared.Models.ImportObjectsJob));

        return await ExecuteAsync(context, job, stoppingToken);
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, Shared.Models.ImportObjectsJob job, CancellationToken stoppingToken)
    {
        if (_job != null) throw new Exception("Can't reuse job instance");

        _job = job;

        _remoteFile = await _connection.Filter<RemoteFile>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, _job.SourceRemoteFileId)
            .Eq(x => x.EntityId, _job.EntityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (_remoteFile == null) throw NotFoundException.New(nameof(RemoteFile));

        _objectType = await _objectTypeService.GetAsync<ObjectTypeWithImportOptions>(context, _job.TargetObjectType);
        if (_objectType == null) throw NotFoundException.New(nameof(_objectType));

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, _job.EntityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (user == null) throw new ForbiddenException(nameof(User));

        _context = user.Context.WithActorFrom(context);

        var index = 0;
        var errors = 0;

        using var memStream = new MemoryStream();
        await using var writer = new StreamWriter(memStream);
        using var csvWriter = new CsvWriter(writer, true);

        var stream = await _remoteFileService.GetStreamAsync(_context, _remoteFile);
        try
        {
            _parser = SpreadsheetFileParser.Create(_remoteFile.ContentType, _remoteFile.Name, stream);
            if (_parser == null) throw new BadRequestException("Unsupported file type");

            _sourceColumns = _job.Mapping
                .Where(x => x.Value is string str && str.StartsWith("{{SOURCE.["))
                .ToDictionary(x => x.Key, x => _parser.Columns[((string)x.Value)[9..^3]]);

            _staticColumns = _job.Mapping
                .Where(x => !_sourceColumns.ContainsKey(x.Key))
                .ToArray();

            _sourceToReferenceFields = new List<SourceToReferenceField>();
            foreach (var fieldName in _sourceColumns.Keys)
            {
                var field = _objectType.Fields[fieldName];
                if (field.Field is not ReferenceField referenceField) continue;

                var sourceToReference = new SourceToReferenceField
                {
                    Field = referenceField,
                    ObjectType = await _objectTypeService.GetAsync(_context, referenceField.ReferenceFieldOptions.ObjectType),
                };

                sourceToReference.CanImplicitlyCreate = sourceToReference.ObjectType.RBAC.Can(_context, ObjectTypePermission.Create);
                _sourceToReferenceFields.Add(sourceToReference);
            }

            _logger.LogInformation("Using {Columns} = {Indices}", string.Join(", ", _sourceColumns.Keys), string.Join(", ", _sourceColumns.Values));

            var mappedColumns = _job.Mapping
                .Where(x => x.Value is string str && str.StartsWith("{{SOURCE.["))
                .DistinctBy(x=>x.Value)
                .ToDictionary(x => ((string)x.Value)[9..^2], x => _parser.Columns[((string)x.Value)[9..^3]]);

            // write header 
            writeRecord(mappedColumns.Keys.Append("[[RESULT]]").Append("[[ID]]"));
            // csvWriter.Context.HasHeaderBeenWritten = true;

            var rows = await _parser.GetRowsAsync();
            await foreach (var record in rows.ReadAllAsync(stoppingToken))
            {
                for (var c = 0; c < record.Length; c++)
                {
                    var value = record[c];
                    if (value is string str && string.IsNullOrWhiteSpace(str)) record[c] = null;
                }
                
                var row = mappedColumns.Select(x => record[x.Value]);

                var result = await ParseRowAsync(index, record);
                writeRecord(row.Append(result.IsError ? $"ERROR: {result.Status}" : result.Status).Append(result.Value));

                if (result.IsError) errors++;
                index++;
            }
        }
        finally
        {
            stream?.Close();
        }

        await writer.FlushAsync();

        var logFile = await UploadLogFileAsync(memStream);

        var updatedJob = await _connection.Filter<Shared.Models.ImportObjectsJob>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, _job.Id)
            .Update
            .Set(x => x.EndedOn, DateTime.UtcNow)
            .SetOrUnset(x => x.OutputRemoteFileId, logFile?.Id)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        if (updatedJob != null)
        {
            _job = updatedJob;
        }

        return new JobResult
        {
            Message = errors > 0 ?
                (index > errors ? $"Imported with {errors} errors" : "Process finished without importing any objects") :
                "Success",
            Result = new Dictionary<string, object>
            {
                { "Errors", errors },
                { "Total", index },
                { "Successes", index - errors },
            }
        };

        void writeRecord(IEnumerable<object> row)
        {
            foreach (var col in row)
            {
                csvWriter.WriteField(col);
            }

            csvWriter.NextRecord();
        }
    }

    private async Task<RemoteFile> UploadLogFileAsync(MemoryStream stream)
    {
        var filename = $"{_remoteFile.Id:N}_output.csv";

        var folder = await _connection.Filter<RemoteFolder>()
            .Eq(x => x.AccountId, _context.AccountId)
            .Eq(x => x.Id, _remoteFile.ParentId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (folder == null)
        {
            _logger.LogError("Failed to find {RemoteFolderId}", _remoteFile.ParentId);
            return null;
        }

        stream.Seek(0, SeekOrigin.Begin);
        var remoteFile = await _remoteFileService.UploadAsync(_context, folder, stream, filename, "text/csv");
        remoteFile.Name = filename;
        remoteFile.Description = $"Output of importing {_objectType.Name} from {_remoteFile.Name}";
        // remoteFile.FlowId = uploadFileOptions.RemoteFileFlowId;
        // remoteFile.ObjectStatusId = uploadFileOptions.RemoteFileObjectStatusId;
        remoteFile.RBAC = new RBAC<RemoteFilePermission>
        {
            [_context.UserId.Value] = RemoteFilePermission.Read
        };
        remoteFile.Refs ??= new List<KeyValuePair<string, object>>(_remoteFile.Refs);
        remoteFile.Refs.Add(new KeyValuePair<string, object>(nameof(RemoteFile), _remoteFile.Id));
        remoteFile.EntityId = _context.UserId.Value;
        remoteFile.AllowAnonymousDownload = true;

        await _connection.InsertAsync(remoteFile);

        await _objectTypeService.FireCreateEventAsync(_context, remoteFile, e =>
        {
            e.Description ??= $"{filename} Uploaded to {_objectType.Name}";
            e.Action ??= "ObjectCreated";
            e.TryAddMetaValue(nameof(PI.Shared.Models.ObjectType), _objectType.Name);
        });

        return remoteFile;

        string addOrReplaceFileExtension(string path, string newExtension)
        {
            var index = path.LastIndexOf('.');
            if (index > 0 && path.IndexOf('/', index) > 0) index = -1;
            return index > 0 ? $"{path[..index]}{newExtension}" : $"{path}{newExtension}";
        }
    }

    private async Task<Result<Guid?>> ParseRowAsync(int index, object[] record)
    {
        var row = new Dictionary<string, object>(_staticColumns);
        foreach (var kv in _sourceColumns)
        {
            var cellValue = record[kv.Value];
            if (cellValue == null) continue;

            // TODO: parse value depending on the type of field (e.g. ReferenceField, SelectField, ...)
            // ...

            row[kv.Key] = cellValue;
        }

        foreach (var source in _sourceToReferenceFields)
        {
            if (!row.TryGetValue(source.Field.Name, out var rawValue) || rawValue is not string strValue) continue;

            if (source.Resolved.TryGetValue(strValue, out var previousId))
            {
                if (previousId == null)
                {
                    _logger.LogError("Can't resolve {Field}", source.Field.Name);
                    return Result<Guid?>.Error($"Can't Resolve/Create {source.Field.Name} with \"{strValue}\"");
                }

                row[source.Field.Name] = previousId;
                continue;
            }

            var knownValues = new Dictionary<string, object>();
            if (row.TryGetValue(nameof(EntityOwnedModel.EntityId), out var entityId))
            {
                // little hack to handle the admin doing things for the managers
                // there is probably a more generic way but may involve a lot more config
                knownValues.Add(nameof(EntityOwnedModel.EntityId), entityId);
            }

            var foreignFieldName = source.Field.ReferenceFieldOptions.ForeignFieldName ?? nameof(Model.Name);
            knownValues[foreignFieldName] = strValue;

            var found = await _objectTypeService.FindOrCreateUsingUniqueIndicesAsync(_context, source.ObjectType, knownValues, foreignFieldName);
            if (found != null && found.TryGetGuidParam(Model.IdFieldName, out var id))
            {
                row[source.Field.Name] = id;
                source.Resolved.TryAdd(strValue, id);
                continue;
            }

            _logger.LogError("Can't create {ObjectType} for {Field}={Value}", source.ObjectType.Name, foreignFieldName, rawValue);
            return Result<Guid?>.Error($"Can't Resolve/Create {source.Field.Name} with \"{strValue}\"");
        }

        _logger.LogInformation("{Row}: {Values}", index, string.Join(", ", row.Select(x => $"{x.Key}={x.Value}")));
        var result = await _objectTypeService.AddObjectAsync(_context, _objectType, row, new ObjectTypeService.AddObjectOptions
        {
            IsUpsert = _objectType.RBAC.Can(_context, ObjectTypePermission.Update),
            IsImporting = true,
        });

        if (result.IsSuccess)
        {
            var id = result.Value.ObjectId;
            _logger.LogInformation("{ContactId}: {Status}", id, result.Status);
            return Result.Success<Guid?>(id, result.Status);
        }

        _logger.LogError("Failed: {Status}", result.Status);
        return result.ConvertTo<Guid?>();
    }

    public class Configuration
    {
        public Guid ImportJobId { get; set; }
    }

    private class SourceToReferenceField
    {
        public ReferenceField Field { get; set; }
        public ObjectType ObjectType { get; set; }
        public Dictionary<string, object> Resolved { get; set; } = new();
        public bool CanImplicitlyCreate { get; set; }
    }
}