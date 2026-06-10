using System;
using System.Dynamic;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using HandlebarsDotNet;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Models.Files;
using PI.Shared.Services;

namespace PI.Files.Services;

public class CopyFileActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly RemoteFileService _remoteFileService;

    public CopyFileActionService(
        ILogger<CopyFileActionService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        RemoteFileService remoteFileService
    ) :
        base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
        _remoteFileService = remoteFileService;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.CopyFile));
        mapper.Register<SimpleActionMessage<CopyFileActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case SimpleActionMessage<CopyFileActionOptions> action:
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

    private async Task ProcessMessageAsync(SimpleActionMessage<CopyFileActionOptions> action)
    {
        using var scope = Logger.AddScope(new
        {
            action.Event.ObjectType,
            action.Event.TargetId,
        });

        if (action.Event.ObjectType != nameof(RemoteFile))
        {
            Logger.LogError($"Copy File Error. ObjectType \"{action.Event.ObjectType}\" not supported");
            return;
        }

        Logger.LogInformation("Copy file");

        try
        {
            Result<RemoteFile> result = await CopyFileAsync(action);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to copy file");
        }
        
        // TODO: fire event?
    }

    private async Task<Result<RemoteFile>> CopyFileAsync(SimpleActionMessage<CopyFileActionOptions> action)
    {
        var sourceFile = await _connection.Filter<RemoteFile>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.TargetId)
            .FirstOrDefaultAsync();

        if (sourceFile == null)
        {
            return Result.Error<RemoteFile>("File does not exist");
        }

        var bucket = await _connection.Filter<RemoteFileBucket>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Options.RemoteFileBucketId)
            .FirstOrDefaultAsync();

        if (bucket == null)
        {
            return Result.Error<RemoteFile>("Invalid or missing bucket");
        }

        var templateContext = await BuildHandlebarsContext(action.Event);
        var remotePath = Handlebars.Compile(action.Options.RemotePath).Invoke(templateContext);
        var fileName = Handlebars.Compile(action.Options.FileName).Invoke(templateContext);
        if (string.IsNullOrWhiteSpace(remotePath) || string.IsNullOrWhiteSpace(fileName))
        {
            return Result.Error<RemoteFile>("Couldn't resolve remote folder/file name");
        }

        var context = new AccountContext(action.Event.AccountId);

        // get/create folder 
        var folder = await _remoteFileService.CreateFolderRecursivelyAsync(context, bucket, remotePath);
        if (folder == null)
        {
            return Result.Error<RemoteFile>($"Failed to get/create folder: {remotePath}");
        }

        var destinationFile = await _remoteFileService.CopyFileAsync(context, sourceFile, folder, fileName);

        // TODO: load object type and fallback to flowid/objectstatus values from it for file?
        // ...
        destinationFile.FlowId = action.Options.RemoteFileFlowId;
        destinationFile.ObjectStatusId = action.Options.RemoteFileObjectStatusId;

        await _connection.InsertAsync(destinationFile);

        await _objectTypeService.FireCreateEventAsync(context, destinationFile, e =>
        {
            e.Description ??= $"File copied";
            e.Action ??= "ObjectCreated";

            e.AddRefValues(action.Event.Refs);

            foreach (var kvp in action.Event.Meta)
            {
                e.TryAddMetaValue(kvp.Key, kvp.Value);
            }

            e.SetMetaValue(nameof(RemoteFile.Name), destinationFile.Name);
            e.SetMetaValue(nameof(RemoteFile.RelativePath), destinationFile.RelativePath);
            e.SetMetaValue(nameof(RemoteFile.AbsoluteUri), destinationFile.AbsoluteUri);
        });

        return Result.Success(destinationFile);
    }

    private async Task<ExpandoObject> BuildHandlebarsContext(FlowEvent evt)
    {
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.Id, evt.RunId)
            .FirstOrDefaultAsync();

        return flowRun.BuildHandlebarsContext(evt);
    }
}