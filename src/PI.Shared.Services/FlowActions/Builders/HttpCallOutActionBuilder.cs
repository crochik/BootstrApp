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

namespace FlowActions;

public class HttpCallOutActionBuilder : AbstractFlowActionBuilder<HttpCallOutActionOptions, SimpleActionMessage<HttpCallOutActionOptions>>
{
    public override Guid Id => ActionIds.HttpCallOut;
    public override string Name => "Make HTTP request";
    public override string[] InputObjectTypes => null;

    public HttpCallOutActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        if (options is not HttpCallOutActionOptions opts)
        {
            throw new BadRequestException("Invalid options for action");
        }

        return new SimpleActionMessage<HttpCallOutActionOptions>(evt, opts);
    }

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, HttpCallOutActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new TextField
            {
                Name = nameof(HttpCallOutActionOptions.Url).ToCamelCase(),
                Label = "Url (template)",
                DefaultValue = opts?.Url,
                TextFieldOptions = new TextFieldOptions
                {
                    AllowExpressions = true,
                }
            };

            yield return new TextField
            {
                Name = nameof(HttpCallOutActionOptions.Body).ToCamelCase(),
                Label = "Body (template)",
                DefaultValue = opts?.Body,
                TextFieldOptions = new TextFieldOptions
                {
                    AllowExpressions = true,
                    Multline = true,
                }
            };
            
            yield return new DictionaryField
            {
                Name = nameof(HttpCallOutActionOptions.Headers).ToCamelCase(),
                Label = "Headers (template)",
                DictionaryFieldOptions =
                {
                    KeyField = new TextField
                    {
                        Name = $"{nameof(HttpCallOutActionOptions.Headers).ToCamelCase()}Key",
                        Label = "Header",
                    },
                    ValueField = new TextField
                    {
                        Name = $"{nameof(HttpCallOutActionOptions.Headers).ToCamelCase()}Value",
                        Label = "Value",
                    },
                    ExpandAllKeys = true,
                },
                DefaultValue = opts?.Headers,
            };              
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, HttpCallOutActionOptions options)
    {
        step.Description = $"Make HTTP Request to {options.Url}";
        
        var (evt1, output1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"Success", $"HTTP Request successful", NextEventName);
        options.NextEventId = evt1.Id;
        options.Output = new[]
        {
            output1,
        };
    }
}