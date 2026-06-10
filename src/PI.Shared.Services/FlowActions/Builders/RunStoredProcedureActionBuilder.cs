using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

public class RunStoredProcedureActionBuilder : AbstractFlowActionBuilder<RunStoredProcedureActionOptions, RunStoredProcedureAction.Message>
{
    public override Guid Id => ActionIds.RunStoredProcedure;
    public override string Name => "Run Stored Procedure";
    public override string[] InputObjectTypes => null;

    public RunStoredProcedureActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new RunStoredProcedureAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, RunStoredProcedureActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new TextField
            {
                Name = nameof(RunStoredProcedureActionOptions.StoredProcedure).ToCamelCase(),
                Label = "Stored Procedure",
                IsRequired = true,
                DefaultValue = opts?.StoredProcedure,
            };
            yield return new DictionaryField
            {
                Name = nameof(RunStoredProcedureActionOptions.Parameters).ToCamelCase(),
                Label = "Parameters",
                DictionaryFieldOptions =
                {
                    KeyField = new TextField
                    {
                        Name = $"{nameof(RunStoredProcedureActionOptions.Parameters).ToCamelCase()}Key",
                        // Visible = new[] { "false" }
                    },
                    ValueField = new TextField
                    {
                        Name = $"{nameof(RunStoredProcedureActionOptions.Parameters).ToCamelCase()}Value",
                        // Visible = new[] { "false" }
                    },
                    // KeyFieldName = $"{nameof(RunStoredProcedureActionOptions.Parameters).ToCamelCase()}Key",
                    // ValueFieldName = $"{nameof(RunStoredProcedureActionOptions.Parameters).ToCamelCase()}Value",
                    ExpandAllKeys = true
                }
            };
            // yield return new TextField
            // {
            //     Name = $"{nameof(RunStoredProcedureActionOptions.Parameters).ToCamelCase()}Key",
            //     Visible = new[] { "false" }
            // };
            // yield return new TextField
            // {
            //     Name = $"{nameof(RunStoredProcedureActionOptions.Parameters).ToCamelCase()}Value",
            //     Visible = new[] { "false" }
            // };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, RunStoredProcedureActionOptions options)
    {
        step.Description = $"Run Stored Procedure '{options.StoredProcedure}'";
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"Run Stored Procedure '{options.StoredProcedure}'", $"{options.StoredProcedure} executed", NextEventName);
        var (evt2, output2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"Failed to Run Stored Procedure '{options.StoredProcedure}'", $"{options.StoredProcedure} failed", ErrorEventName);
        options.NextEventId = evt.Id;
        options.ErrorEventId = evt2.Id;
        options.Output = new[]
        {
            output,
            output2,
        };
    }
}