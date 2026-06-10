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

public class ExportToSalesforceActionBuilder : AbstractFlowActionBuilder<ExportToSalesforceActionOptions, ExportToSalesforceAction.Message>
{
    public override string Name => "Export to Salesforce";

    public override Guid Id => ActionIds.ExportToSalesforce;

    public override string IconName => IntegrationIds.Salesforce.ToString();

    public override string[] InputObjectTypes => new[]
    {
        nameof(Lead),
        nameof(Appointment),
    };

    public ExportToSalesforceActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new ExportToSalesforceAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, ExportToSalesforceActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new TextField
            {
                Name = nameof(ExportToSalesforceActionOptions.ObjectType).ToCamelCase(),
                Label = "Salesforce Object Type",
                DefaultValue = opts?.ObjectType,
            };
            yield return new CheckboxField
            {
                Name = nameof(ExportToSalesforceActionOptions.MapAllFields).ToCamelCase(),
                Label = "Auto Map Properties",
                DefaultValue = opts?.MapAllFields ?? true,
            };
            yield return new CheckboxField
            {
                Name = nameof(ExportToSalesforceActionOptions.ForcePlainPhoneNumber).ToCamelCase(),
                Label = "Use Plain Phone Number",
                DefaultValue = opts?.ForcePlainPhoneNumber ?? true,
            };
            yield return new DictionaryField
            {
                Name = nameof(ExportToSalesforceActionOptions.PropertiesMapping).ToCamelCase(),
                Label = "Mapping",
                DictionaryFieldOptions =
                {
                    KeyField = new TextField
                    {
                        Name = $"{nameof(ExportToSalesforceActionOptions.PropertiesMapping).ToCamelCase()}Key",
                        Label = "Salesforce Property",
                    },
                    ValueField = new TextField
                    {
                        Name = $"{nameof(ExportToSalesforceActionOptions.PropertiesMapping).ToCamelCase()}Value",
                        Label = "Value",
                    },
                    ExpandAllKeys = true,
                },
                DefaultValue = opts?.PropertiesMapping,
            };            
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, ExportToSalesforceActionOptions options)
    {
        var integration = "Salesforce";

        step.Description = $"Export {flow.ObjectType} to {integration}";
        
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"Export to {integration}", $"{flow.ObjectType} exported to {integration}", NextEventName);
        var (evt2, output2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"Export to {integration} failed", $"Failed to export {flow.ObjectType} to {integration}", ErrorEventName);
        options.NextEventId = evt.Id;
        options.ErrorEventId = evt2.Id;
        options.Output = new[]
        {
            output,
            output2,
        };
    }
}