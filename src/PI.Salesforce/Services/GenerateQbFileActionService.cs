using System;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using HandlebarsDotNet;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Salesforce.IIF;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Models.Files;
using PI.Shared.Salesforce.Models;
using PI.Shared.Services;

namespace Services;

public class GenerateQbFileActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly RemoteFileService _remoteFileService;

    public GenerateQbFileActionService(ILogger<GenerateQbFileActionService> logger, IConfiguration configuration, IMessageBroker messageBroker,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        RemoteFileService remoteFileService
    )
        : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
        _remoteFileService = remoteFileService;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.GenerateQbFile));
        mapper.Register<SimpleActionMessage<GenerateQbFileActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case SimpleActionMessage<GenerateQbFileActionOptions> msg:
                    await ProcessMessageAsync(msg);
                    break;
            }
        }
        finally
        {
            evt.Acknowledge();
        }
    }

    private async Task ProcessMessageAsync(SimpleActionMessage<GenerateQbFileActionOptions> action)
    {
        var error = default(string);
        try
        {
            var result = await GenerateFileAsync(action);
            if (result.IsSuccess)
            {
                var evt = new GenericFlowEvent(action.Event)
                {
                    Description = result.Status ?? "Quickbooks IIF File generated successfully.",
                    EventTypeId = action.Options.NextEventId,
                };
                evt.AddRefValue(result.Value);
                await MessageBroker.DispatchAsync(evt);
                return;
            }

            error = result.Status;
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }


        if (!action.Options.ErrorEventId.HasValue)
        {
            var errorEvent = new GenericFlowEvent(action.Event)
            {
                Description = error ?? "Quickbooks IIF File generated successfully.",
                EventTypeId = action.Options.NextEventId,
            };
            await MessageBroker.DispatchAsync(errorEvent, true);
        }
        else
        {
            var errorEvent = new GenericFlowEvent(action.Event)
            {
                Description = error ?? "Quickbooks IIF File generated successfully.",
                EventTypeId = action.Options.ErrorEventId,
            };
            await MessageBroker.DispatchAsync(errorEvent);
        }
    }

    private async Task<ExpandoObject> BuildHandlebarsContext(FlowEvent evt)
    {
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.Id, evt.RunId)
            .FirstOrDefaultAsync();

        return flowRun.BuildHandlebarsContext(evt);
    }

    private async Task<Result<RemoteFile>> GenerateFileAsync(SimpleActionMessage<GenerateQbFileActionOptions> action)
    {
        using var scope = Logger.AddScope(new
        {
            action.Event.ObjectType,
            action.Event.TargetId,
            action.Options.RemoteFileBucketId,
            action.Options.RemotePath,
            action.Options.FileName,
        });

        if (action.Event.ObjectType != SfOption.ObjectTypeName)
        {
            return Result.Error<RemoteFile>("Invalid Object Type");
        }

        Logger.LogInformation("Generate Qb file");

        var option = await _connection.Filter<SalesforceCustomObject>(SfOptionObject.CollectionName)
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.TargetId)
            .FirstOrDefaultAsync();

        if (option == null)
        {
            Logger.LogError("{OptionId} not found", action.Event.TargetId);
            return Result.Error<RemoteFile>("Option not found");
        }

        if (!option.TryGetProperty<string>("ParentProject__c", out var parentWorkOrderId) || string.IsNullOrWhiteSpace(parentWorkOrderId))
        {
            Logger.LogError("ParentProject__c not found");
            return Result.Error<RemoteFile>("Could not figure out project id");
        }

        var workOrder = await _connection.Filter<CustomObject>("salesforce.WorkOrder")
            .Eq(x => x.AccountId, option.AccountId)
            .Eq(x => x.ExternalId, parentWorkOrderId)
            .IncludeField(x => x.EntityId)
            .FirstOrDefaultAsync();

        if (workOrder == null)
        {
            Logger.LogError("Failed to load {WorkOrderExternalId}", parentWorkOrderId);
            return Result.Error<RemoteFile>($"Could not find project: {parentWorkOrderId}");
        }
        
        Logger.LogInformation("Generating file for {WorkOrderExternalId} {WorkOrderId}", parentWorkOrderId, workOrder.Id);

        try
        {
            var generator = new QbProposalGenerator(_connection);
            var content = await generator.GenerateAsync(option.ExternalId);
            
            var bucket = await _connection.Filter<RemoteFileBucket>()
                .Eq(x => x.AccountId, action.Event.AccountId)
                .Eq(x => x.Id, action.Options.RemoteFileBucketId)
                .FirstOrDefaultAsync();

            if (bucket == null)
            {
                Logger.LogError("Invalid or missing {RemoteFileBucketId}", action.Options.RemoteFileBucketId);
                return Result.Error<RemoteFile>("Invalid or missing bucket");
            }

            var templateContext = await BuildHandlebarsContext(action.Event);
            var remotePath = Handlebars.Compile(action.Options.RemotePath).Invoke(templateContext);
            var fileName = Handlebars.Compile(action.Options.FileName).Invoke(templateContext);
            if (string.IsNullOrWhiteSpace(remotePath) || string.IsNullOrWhiteSpace(fileName))
            {
                Logger.LogError("Failed to resolve {RemotePath} or {FileName}", action.Options.RemotePath, action.Options.FileName);
                return Result.Error<RemoteFile>("Couldn't resolve remote folder/file name");
            }

            var context = new AccountContext(option.AccountId);

            // get/create folder 
            var folder = await _remoteFileService.CreateFolderRecursivelyAsync(context, bucket, remotePath);
            if (folder == null)
            {
                Logger.LogError("Failed to create {RemotePath}", remotePath);
                return Result.Error<RemoteFile>($"Failed to get/create folder: {remotePath}");
            }

            // create file
            var stream = new MemoryStream(Encoding.ASCII.GetBytes(content));
            var file = await _remoteFileService.UploadAsync(context, folder, stream, fileName, "text/plain");
            if (file == null)
            {
                Logger.LogError("Failed to create {FileName} in {RemoteFolderId}", fileName, folder.Id);
                return Result.Error<RemoteFile>($"Failed to create file: {fileName}");
            }

            // TODO: load object type and fallback to flowid/objectstatus values from it for file?
            // ...
            file.EntityId = workOrder.EntityId;
            file.FlowId = action.Options.RemoteFileFlowId;
            file.ObjectStatusId = action.Options.RemoteFileObjectStatusId;

            await _connection.InsertAsync(file);

            await _objectTypeService.FireCreateEventAsync(context, file, e =>
            {
                e.Description ??= $"Quickbooks IIF file Created";
                e.Action ??= "ObjectCreated";

                e.AddRefValues(action.Event.Refs);
                
                foreach (var kvp in action.Event.Meta)
                {
                    e.TryAddMetaValue(kvp.Key, kvp.Value);
                }
                
                e.SetRefValue("sf_WorkOrder", workOrder.Id);

                e.SetMetaValue(nameof(RemoteFile.Name), file.Name);
                e.SetMetaValue(nameof(RemoteFile.RelativePath), file.RelativePath);
                e.SetMetaValue(nameof(RemoteFile.AbsoluteUri), file.AbsoluteUri);
                e.SetMetaValue("WorkOrderExternalId", parentWorkOrderId);
            });

            return Result.Success(file);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception creating/uploading iif file");
            return Result.Error<RemoteFile>(ex.Message);
        }
    }
}