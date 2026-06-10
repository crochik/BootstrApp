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

public class SpreadsheetToCatalogActionBuilder : AbstractFlowActionBuilder<SpreadsheetToCatalogActionOptions, SpreadsheetToCatalogAction.Message>
{
    public override Guid Id => ActionIds.SpreadsheetToCatalog;

    public override string Name => "Update Catalog with Spreadsheet";

    public override string[] InputObjectTypes => new[] { nameof(Spreadsheet) };

    public SpreadsheetToCatalogActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new SpreadsheetToCatalogAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, SpreadsheetToCatalogActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new TextField
            {
                Name = nameof(SpreadsheetToCatalogActionOptions.StoredProcedure).ToCamelCase(),
                Label = "Stored Procedure",
                IsRequired = true,
                DefaultValue = opts?.StoredProcedure,
            };
            yield return new CheckboxField
            {
                Name = nameof(SpreadsheetToCatalogActionOptions.IsToProduction).ToCamelCase(),
                Label = "Publish Changes to Catalog",
                DefaultValue = opts?.IsToProduction
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, SpreadsheetToCatalogActionOptions options)
    {
        step.Description = "Merge spreadsheet into Product Catalog";
        
        var (evt1, output1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Merge Spreadsheet Success", $"Spreadsheet merged without errors", NextEventName);
        var (evt2, output2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Merge Spreadsheet Partial", $"Spreadsheet merged with some errors", "partial");
        var (evt3, output3) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Merge Spreadsheet Failed", $"Spreadsheet merge failed", ErrorEventName);
        options.SuccessEventId = evt1.Id;
        options.WithErrorsEventId = evt2.Id;
        options.FailedEventId = evt3.Id;
        options.Output = new[]
        {
            output1,
            output2,
            output3,
        };
    }
}