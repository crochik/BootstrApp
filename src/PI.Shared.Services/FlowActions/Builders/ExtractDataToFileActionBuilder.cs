using System;
using System.Collections;
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

public class ExtractDataToFileActionBuilder : AbstractFlowActionBuilder<ExtractDataToFileActionOptions, SimpleActionMessage<ExtractDataToFileActionOptions>>
{
    public override Guid Id => ActionIds.ExtractDataToFile;
    public override string Name => "Extract Data to file";
    public override string[] InputObjectTypes => null;
 
    public ExtractDataToFileActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        if (options is not ExtractDataToFileActionOptions opts)
        {
            throw new BadRequestException("Invalid options for action");
        }

        return new SimpleActionMessage<ExtractDataToFileActionOptions>(evt, opts);
    }

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, ExtractDataToFileActionOptions opts = null)
    {
        return ValueTask.FromResult(fields());

        IEnumerable<FormField> fields()
        {
            yield return new SelectField
            {
                Name = nameof(ExtractDataToFileActionOptions.Source).ToCamelCase(),
                Label = "Source",
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = new Dictionary<string,string>
                    {
                        { "Postgres", "Postgres" }
                    }
                },
                DefaultValue = opts?.Source ?? "Postgres",
            };
            
            yield return new TextField
            {
                Name = nameof(ExtractDataToFileActionOptions.Query).ToCamelCase(),
                Label = "Query",
                TextFieldOptions = new TextFieldOptions
                {
                    Multline = true,
                },
                DefaultValue = opts?.Query
            };
            
            yield return new DictionaryField
            {
                Name = nameof(ExtractDataToFileActionOptions.Parameters).ToCamelCase(),
                Label = "Parameters",
                DictionaryFieldOptions =
                {
                    KeyField = new TextField
                    {
                        Name = $"{nameof(ExtractDataToFileActionOptions.Parameters).ToCamelCase()}Key",
                        Label = "Query Parameter",
                    },
                    ValueField = new TextField
                    {
                        Name = $"{nameof(ExtractDataToFileActionOptions.Parameters).ToCamelCase()}Value",
                        Label = "Value",
                    },
                    ExpandAllKeys = true,
                },
                DefaultValue = opts?.Parameters,
            };    
            
            yield return new ReferenceField
            {
                Name = nameof(ExtractDataToFileActionOptions.RemoteFileBucketId).ToCamelCase(),
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
                Name = nameof(ExtractDataToFileActionOptions.RemotePath).ToCamelCase(),
                Label = "Remote Path (template)",
                DefaultValue = opts?.RemotePath
            };

            yield return new TextField
            {
                Name = nameof(ExtractDataToFileActionOptions.FileName).ToCamelCase(),
                Label = "File Name (template)",
                DefaultValue = opts?.FileName
            };

            yield return new ReferenceField
            {
                Name = nameof(ExtractDataToFileActionOptions.RemoteFileFlowId).ToCamelCase(),
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
                Name = nameof(ExtractDataToFileActionOptions.RemoteFileObjectStatusId).ToCamelCase(),
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
            
            yield return new CheckboxField
            {
                Name = nameof(ExtractDataToFileActionOptions.AllowAnonymousDownload).ToCamelCase(),
                Label = "Allow Anonymous Download",
                DefaultValue = opts?.AllowAnonymousDownload
            };            
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, ExtractDataToFileActionOptions options)
    {
        var (evt1, out1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "DataExtracted", "Data Extracted", NextEventName);
        var (evt2, out2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "ErrorExtractingData", "Failed to extract Data", ErrorEventName);
        step.Description = "Extracted Data to file";
        options.NextEventId = evt1.Id;
        options.ErrorEventId = evt2.Id;
        options.Output = new[]
        {
            out1,
            out2,
        };
    }
}