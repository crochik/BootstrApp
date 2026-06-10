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
using PI.Shared.Salesforce.Models;

namespace FlowActions;

[Obsolete]
public class GenerateQbFileActionBuilder : AbstractFlowActionBuilder<GenerateQbFileActionOptions, SimpleActionMessage<GenerateQbFileActionOptions>>
{
    public override Guid Id => ActionIds.GenerateQbFile;
    public override string Name => "Generate IIF file for Option";

    public override string[] InputObjectTypes => new[]
    {
        SfOption.ObjectTypeName,
    };

    public GenerateQbFileActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        if (options is not GenerateQbFileActionOptions opts)
        {
            throw new BadRequestException("Invalid options for action");
        }

        return new SimpleActionMessage<GenerateQbFileActionOptions>(evt, opts);
    }

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, GenerateQbFileActionOptions opts = null)
    {
        return ValueTask.FromResult(fields());

        IEnumerable<FormField> fields()
        {
            yield return new ReferenceField
            {
                Name = nameof(GenerateQbFileActionOptions.RemoteFileBucketId).ToCamelCase(),
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
                Name = nameof(GenerateQbFileActionOptions.RemotePath).ToCamelCase(),
                Label = "Remote Path (template)",
                DefaultValue = opts?.RemotePath
            };

            yield return new TextField
            {
                Name = nameof(GenerateQbFileActionOptions.FileName).ToCamelCase(),
                Label = "File Name (template)",
                DefaultValue = opts?.FileName
            };

            yield return new ReferenceField
            {
                Name = nameof(GenerateQbFileActionOptions.RemoteFileFlowId).ToCamelCase(),
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
                Name = nameof(GenerateQbFileActionOptions.RemoteFileObjectStatusId).ToCamelCase(),
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

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, GenerateQbFileActionOptions options)
    {
        var (evt1, out1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "IifGenerated", "IIF File generated", NextEventName);
        var (evt2, out2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "ErrorGeneratingIif", "Failed to generate IIF", ErrorEventName);
        step.Description = $"Generate IIF file for Option";
        options.NextEventId = evt1.Id;
        options.ErrorEventId = evt2.Id;
        options.Output = new[]
        {
            out1,
            out2,
        };
    }
}