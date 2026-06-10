using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using CsvHelper;
using HandlebarsDotNet;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Postgres;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Files;
using PI.Shared.Services;

namespace Reports.Services;

public class ExtractDataToFileActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly PostgresConnection _postgresConnection;
    private readonly MongoConnection _connection;
    private readonly RemoteFileService _remoteFileService;
    private readonly ObjectTypeService _objectTypeService;

    public ExtractDataToFileActionService(
        ILogger<ExtractDataToFileActionService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        PostgresConnection postgresConnection,
        MongoConnection connection,
        RemoteFileService remoteFileService,
        ObjectTypeService objectTypeService
    ) : base(logger, configuration, messageBroker)
    {
        _postgresConnection = postgresConnection;
        _connection = connection;
        _remoteFileService = remoteFileService;
        _objectTypeService = objectTypeService;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.ExtractDataToFile));
        mapper.Register<SimpleActionMessage<ExtractDataToFileActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case SimpleActionMessage<ExtractDataToFileActionOptions> action:
                    await ProcessMessageAsync(action);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message");
        }
        finally
        {
            evt.Acknowledge();
        }
    }

    private async Task ProcessMessageAsync(SimpleActionMessage<ExtractDataToFileActionOptions> action)
    {
        using var scope = Logger.AddScope(new
        {
            action.Event.ObjectType,
            action.Event.TargetId,
        });

        Logger.LogInformation("Extract Data to remote file");

        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.RunId)
            .FirstOrDefaultAsync();

        var flowContext = flowRun.BuildHandlebarsContext(action.Event, context =>
        {
            var now = DateTime.UtcNow;
            context.Add("Action", new
            {
                Intervals = new
                {
                    Now = now,
                    // ...
                },
                Timestamp = new
                {
                    Short = now.ToString("yyMMdd"),
                    Long = now.ToString("yyyyMMddHHmmss"),
                }
            });
        });

        var error = default(string);

        try
        {
            var result = await ExtractDataAndUploadAsync(action, flowContext);
            if (result.IsSuccess)
            {
                // // TODO: add property to option so it can override the name of the output
                // var keyName = $"Output|{result.Value.ObjectType}";
                //
                // await _connection.Filter<FlowRun>()
                //     .Eq(x => x.AccountId, action.Event.AccountId)
                //     .Eq(x => x.Id, action.Event.RunId)
                //     .Update
                //     .Set(x => x.Objects[keyName], new ObjectWithType
                //     {
                //         ObjectType = result.Value.ObjectType,
                //         Object = new Dictionary<string, object>
                //         {
                //             // for now only add id so it can be used as an attachment 
                //             { Model.IdFieldName, result.Value.Id },
                //             
                //             // TODO: actually add object?
                //             // ...
                //         }
                //     })
                //     .UpdateAndGetOneAsync();

                var evt = new GenericFlowEvent(action.Event)
                {
                    Description = result.Status ?? "Data extracted to remote file",
                    EventTypeId = action.Options.NextEventId,
                };
                evt.AddRefValue(result.Value);

                // TODO: add property to option so it can override the name of the output
                evt.SetMetaValue($"Action|Output|{nameof(RemoteFile)}Id", result.Value.Id);

                await MessageBroker.DispatchAsync(evt);
                return;
            }

            error = result.Status;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to extract data and save to remote file");
            error = ex.Message;
        }

        if (!action.Options.ErrorEventId.HasValue)
        {
            var errorEvent = new GenericFlowEvent(action.Event)
            {
                Description = error ?? "Failed to extract data to remote file",
                EventTypeId = action.Options.NextEventId,
            };

            await MessageBroker.DispatchAsync(errorEvent, true);
        }
        else
        {
            var errorEvent = new GenericFlowEvent(action.Event)
            {
                Description = error ?? "Failed to extract data to remote file",
                EventTypeId = action.Options.ErrorEventId,
            };

            await MessageBroker.DispatchAsync(errorEvent);
        }
    }

    private async Task<Result<RemoteFile>> ExtractDataAndUploadAsync(SimpleActionMessage<ExtractDataToFileActionOptions> action, ExpandoObject flowContext)
    {
        var stream = await GenerateCsvAsync(action, flowContext);
        if (stream == null)
        {
            return Result.Error<RemoteFile>("Failed to extract data");
        }

        stream.Seek(0, SeekOrigin.Begin);

        return await SaveFileAsync(action, flowContext, stream);
    }

    private async Task<MemoryStream> GenerateCsvAsync(SimpleActionMessage<ExtractDataToFileActionOptions> action, ExpandoObject flowContext)
    {
        var arguments = new Dictionary<string, object>();
        if (action.Options.Parameters != null)
        {
            foreach (var kvp in action.Options.Parameters)
            {
                if (!flowContext.TryResolvePathValue(kvp.Value, out var value))
                {
                    Logger.LogError("Failed to resolve {Value} for {Argument}", kvp.Value, kvp.Key);
                    // ...
                    return null;
                }

                arguments[kvp.Key] = value;
            }
        }

        var sql = HandlebarsDotNet.Handlebars.Compile(action.Options.Query).Invoke(flowContext);
        await using var cmd = _postgresConnection.CreateCommand(sql, arguments);
        await using var reader = await cmd.ExecuteReaderAsync();
        var columns = await reader.GetColumnSchemaAsync();

        var memStream = new MemoryStream();
        await using var writer = new StreamWriter(memStream, leaveOpen: true);
        using (var csvWriter = new CsvWriter(writer, true))
        {
            // header?
            foreach (var t in columns)
            {
                csvWriter.WriteField(t.ColumnName);
            }

            await csvWriter.NextRecordAsync();

            while (await reader.ReadAsync())
            {
                for (var c = 0; c < columns.Count; c++)
                {
                    var value = reader.GetValue(c);
                    csvWriter.WriteField(value);
                }

                await csvWriter.NextRecordAsync();
            }
        }

        await writer.FlushAsync();
        return memStream;
    }

    private async Task<Result<RemoteFile>> SaveFileAsync(SimpleActionMessage<ExtractDataToFileActionOptions> action, ExpandoObject templateContext, Stream stream)
    {
        var bucket = await _connection.Filter<RemoteFileBucket>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Options.RemoteFileBucketId)
            .FirstOrDefaultAsync();

        if (bucket == null)
        {
            Logger.LogError("Invalid or missing {RemoteFileBucketId}", action.Options.RemoteFileBucketId);
            return Result.Error<RemoteFile>("Invalid or missing bucket");
        }

        var remotePath = Handlebars.Compile(action.Options.RemotePath).Invoke(templateContext);
        var fileName = Handlebars.Compile(action.Options.FileName).Invoke(templateContext);
        if (string.IsNullOrWhiteSpace(remotePath) || string.IsNullOrWhiteSpace(fileName))
        {
            Logger.LogError("Failed to resolve {RemotePath} or {FileName}", action.Options.RemotePath, action.Options.FileName);
            return Result.Error<RemoteFile>("Couldn't resolve remote folder/file name");
        }

        var context = new AccountContext(action.Event.AccountId);

        // get/create folder 
        var folder = await _remoteFileService.CreateFolderRecursivelyAsync(context, bucket, remotePath);
        if (folder == null)
        {
            Logger.LogError("Failed to create {RemotePath}", remotePath);
            return Result.Error<RemoteFile>($"Failed to get/create folder: {remotePath}");
        }

        var file = await _remoteFileService.UploadAsync(context, folder, stream, fileName, "text/csv");
        if (file == null)
        {
            Logger.LogError("Failed to create {FileName} in {RemoteFolderId}", fileName, folder.Id);
            return Result.Error<RemoteFile>($"Failed to create file: {fileName}");
        }

        file.EntityId = action.Event.AccountId;
        file.FlowId = action.Options.RemoteFileFlowId;
        file.ObjectStatusId = action.Options.RemoteFileObjectStatusId;
        file.AllowAnonymousDownload = action.Options.AllowAnonymousDownload;

        file.Refs = (action.Event.Refs ?? Enumerable.Empty<KeyValuePair<string, object>>())
            .Append(new KeyValuePair<string, object>($"{action.Event.ObjectType}Id", action.Event.TargetId))
            .Append(new KeyValuePair<string, object>($"{nameof(FlowRun)}Id", action.Event.RunId))
            .DistinctBy(x => HashCode.Combine(x.Key, x.Value))
            .ToList()
            ;

        await _connection.InsertAsync(file);

        await _objectTypeService.FireCreateEventAsync(context, file, e =>
        {
            e.Description ??= $"Data Extracted";
            e.Action ??= "ObjectCreated";

            e.AddRefValues(action.Event.Refs);

            foreach (var kvp in action.Event.Meta)
            {
                e.TryAddMetaValue(kvp.Key, kvp.Value);
            }

            e.SetRefValue(action.Event.ObjectType, action.Event.TargetId);

            e.SetMetaValue(nameof(RemoteFile.Name), file.Name);
            e.SetMetaValue(nameof(RemoteFile.RelativePath), file.RelativePath);
            e.SetMetaValue(nameof(RemoteFile.AbsoluteUri), file.AbsoluteUri);

            e.SetMetaValue($"Action|Output|{nameof(RemoteFile)}Id", file.Id);
        });

        return Result.Success(file);
    }
}