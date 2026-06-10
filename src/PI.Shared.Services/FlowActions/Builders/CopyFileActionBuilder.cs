using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Files;

namespace FlowActions;

public class CopyFileActionBuilder : AbstractFlowActionBuilder<CopyFileActionOptions, SimpleActionMessage<CopyFileActionOptions>>
{
    public override Guid Id => ActionIds.CopyFile;
    public override string Name => "Copy File to Bucket";

    public override string[] InputObjectTypes => new[]
    {
        nameof(RemoteFile),
        // TODO: allow other targets 
        // ...
    };
        

    public CopyFileActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        if (options is not CopyFileActionOptions opts)
        {
            throw new BadRequestException("Invalid options for action");
        }

        return new SimpleActionMessage<CopyFileActionOptions>(evt, opts);
    }

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, CopyFileActionOptions opts = null)
    {
        return ValueTask.FromResult(fields());

        IEnumerable<FormField> fields()
        {
            // TODO: if the target is not a RemoteFile
            // add field to allow user to enter Path to a RemoteFile id
            // ...
            
            yield return new ReferenceField
            {
                Name = nameof(CopyFileActionOptions.RemoteFileBucketId).ToCamelCase(),
                Label = "Bucket",
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = nameof(RemoteFileBucket),
                    AutoComplete = true,
                },
                DefaultValue = opts?.RemoteFileBucketId
            };

            yield return new TextField
            {
                Name = nameof(CopyFileActionOptions.RemotePath).ToCamelCase(),
                Label = "Remote Path (template)",
                DefaultValue = opts?.RemotePath
            };

            yield return new TextField
            {
                Name = nameof(CopyFileActionOptions.FileName).ToCamelCase(),
                Label = "File Name (template)",
                DefaultValue = opts?.FileName
            };

            yield return new ReferenceField
            {
                Name = nameof(CopyFileActionOptions.RemoteFileFlowId).ToCamelCase(),
                Label = "Generated File Flow",
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = nameof(Flow),
                    Criteria = new[]
                    {
                        Condition.Eq(nameof(Flow.ObjectType), nameof(RemoteFile)),
                    },
                    AutoComplete = true,
                },
                DefaultValue = opts?.RemoteFileFlowId
            };

            yield return new ReferenceField
            {
                Name = nameof(CopyFileActionOptions.RemoteFileObjectStatusId).ToCamelCase(),
                Label = "Generated File Status",
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = nameof(ObjectStatus),
                    Criteria = new[]
                    {
                        Condition.Eq(nameof(Flow.ObjectType), nameof(RemoteFile)),
                    },
                    AutoComplete = true,
                },
                DefaultValue = opts?.RemoteFileObjectStatusId
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, CopyFileActionOptions options)
    {
        var (evt1, out1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "FileCopied", "File Copied", NextEventName);
        var (evt2, out2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "ErrorCopyingFile", "Failed to copy File", ErrorEventName);
        step.Description = $"Copied Remote File";
        options.NextEventId = evt1.Id;
        options.ErrorEventId = evt2.Id;
        options.Output = new[]
        {
            out1,
            out2,
        };
    }
}