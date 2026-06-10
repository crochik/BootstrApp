using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Files;
using PI.Shared.Services;
using PI.Shared.Services.ActionRunners;

namespace PI.Files.ActionRunners;

public enum PresignedUrlType
{
    Download,
    Upload,
}

public class GetPresignedUrlActionOptions : ActionOptions
{
    public const string CreatedEvent = "UrlCreated";
    public const string FailedToCreateEvent = "FailToCreateUrl";

    /// <summary>
    /// Id of remote file to create pre-signed url for (can be template)
    /// </summary>
    public string RemoteFileId { get; set; }

    /// <summary>
    /// Type of url to create
    /// </summary>
    public PresignedUrlType PresignedUrlType { get; set; }
}

public class GetPresignedUrlActionRunner : AbstractRunner<GetPresignedUrlActionOptions>
{
    private readonly ILogger<GetPresignedUrlActionRunner> _logger;
    private readonly MongoConnection _connection;
    private readonly RemoteFileService _service;
    public override Guid ActionId => ActionIds.GetPresignedUrl;

    public GetPresignedUrlActionRunner(ILogger<GetPresignedUrlActionRunner> logger, MongoConnection connection, RemoteFileService service)
    {
        _logger = logger;
        _connection = connection;
        _service = service;
    }

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, GetPresignedUrlActionOptions options)
    {
        var runContext = context.Run.BuildHandlebarsContext(context.Event);
        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.RemoteFileId, out var remoteFileIdObj))
        {
            _logger.LogError("Could not resolve {RemoteFileId}", options.RemoteFileId);
            return buildErrorEvent($"Could not resolve remote file");
        }

        var remoteFileId = remoteFileIdObj switch
        {
            Guid uid => uid,
            string str => Guid.TryParse(str, out var uuid) ? uuid : null,
            _ => default(Guid?)
        };

        if (!remoteFileId.HasValue)
        {
            _logger.LogError("Invalid {RemoteFileId}", options.RemoteFileId);
            return buildErrorEvent("Invalid Id");
        }

        switch (options.PresignedUrlType)
        {
            case PresignedUrlType.Upload:
                break;
            
            case PresignedUrlType.Download:
                // TODO: implement ...
            default:
                return buildErrorEvent("Unexpected Option");
        }

        // UPLOAD
        var result = await _service.GetPresignedUploadUrlAsync(context.EntityContext, remoteFileId.Value);
        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to get presigned {Type} url for {RemoteFileId}: {Status}", options.PresignedUrlType, remoteFileId, result.Status);
            return buildErrorEvent($"Failed To generate PresignedUrl: {result.Status}");
        }

        var output = options.Output.FirstOrDefault(x => x.Name == GetPresignedUrlActionOptions.CreatedEvent);
        if (!(output?.EventId.HasValue ?? false)) return [];

        var evt = new GenericFlowEvent(context.Event)
        {
            Action = nameof(ActionIds.GetPresignedUrl),
            Description = output.Description,
            EventTypeId = output.EventId,
        };
        evt.AddRefValue(nameof(RemoteFile), remoteFileId);
        evt.SetMetaValue($"Action|Output|{nameof(RemoteFile)}Id", remoteFileId);
        evt.SetMetaValue("Action|Output|PresignedUrl", result.Value);

        return [evt];

        FlowEvent[] buildErrorEvent(string message)
        {
            var errorOutput = options.Output.FirstOrDefault(x => x.Name == GetPresignedUrlActionOptions.FailedToCreateEvent);
            if (errorOutput?.EventId.HasValue ?? false)
            {
                return
                [
                    new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.GetPresignedUrl),
                        Description = $"{errorOutput.Description}. {message}",
                        EventTypeId = errorOutput.EventId,
                    }
                ];
            }

            return [];
        }
    }
}