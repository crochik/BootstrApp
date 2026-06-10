using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Billing;
using PI.Shared.Models.Expressions;
using FlowStep = PI.Shared.Models.FlowStep;

namespace FlowActions;

public class CreateInvoiceActionBuilder : AbstractFlowActionBuilder<CreateInvoiceActionOptions, SimpleActionMessage<CreateInvoiceActionOptions>>
{
    public override Guid Id => ActionIds.CreateInvoice;

    public override string Name => "Create Invoice";

    public override string[] InputObjectTypes => new[]
    {
        nameof(Lead),
        // nameof(Appointment),
        // ...
    };

    public CreateInvoiceActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new SimpleActionMessage<CreateInvoiceActionOptions>(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, CreateInvoiceActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new TextField
            {
                Name = nameof(CreateInvoiceActionOptions.Name).ToCamelCase(),
                Label = "Invoice Name",
                IsRequired = true,
                DefaultValue = opts?.Name,
            };
            yield return new TextField
            {
                Name = nameof(CreateInvoiceActionOptions.Description).ToCamelCase(),
                Label = "Invoice Description",
                IsRequired = true,
                DefaultValue = opts?.Description,
            };
            yield return new TextField
            {
                Name = nameof(CreateInvoiceActionOptions.ExternalIdSuffix).ToCamelCase(),
                Label = "Invoice Unique Suffix",
                IsRequired = true,
                DefaultValue = opts?.ExternalIdSuffix,
            };
            yield return new TextField
            {
                Name = nameof(CreateInvoiceActionOptions.EntityId).ToCamelCase(),
                Label = "Entity to be billed",
                IsRequired = true,
                DefaultValue = opts?.EntityId,
            };
            yield return new TextField
            {
                Name = nameof(CreateInvoiceActionOptions.Item).ToCamelCase(),
                Label = "Billable Item Object",
                IsRequired = false,
                DefaultValue = opts?.Item,
            };
            yield return new MultiReferenceField
            {
                Name = nameof(CreateInvoiceActionOptions.AdditionalItems).ToCamelCase(),
                Label = "Additional Items",
                IsRequired = false,
                MultiReferenceFieldOptions = new MultiReferenceFieldOptions
                {
                    ObjectType = nameof(BillableItem),
                    Criteria = new []
                    {
                        Condition.Eq(nameof(BillableItem.Rule), "Additional"), 
                    }
                },
                DefaultValue = opts?.AdditionalItems,
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, CreateInvoiceActionOptions options)
    {
        var (evt1, out1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Create Invoice", "Invoice Created", NextEventName);
        var (evt2, out2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Skip Invoice Creation", "Invoice Creation skipped", "skip");
        step.Description = $"Create Invoice for {flow.ObjectType}";
        options.NextEventId = evt1.Id;
        options.SkipEventId = evt2.Id;
        options.Output = new[]
        {
            out1,
            out2,
        };
    }
}