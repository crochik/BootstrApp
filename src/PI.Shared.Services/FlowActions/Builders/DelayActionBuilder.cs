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
// TODO: create action to clear delays of "key=TAG"
// ...

public class DelayActionBuilder : AbstractFlowActionBuilder<DelayActionOptions, DelayAction.Message>
{
    public override string Name => "Add Delayed Trigger";
    public override Guid Id => ActionIds.DelayEvent;
    public override string IconName => Id.ToString();

    public override string[] InputObjectTypes => null; // any

    public DelayActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new DelayAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, DelayActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new NumberField
            {
                Name = nameof(DelayActionOptions.Amount).ToCamelCase(),
                Label = "Amount",
                IsRequired = true,
                NumberFieldOptions = new NumberFieldOptions
                {
                    DecimalPlaces = 0,
                },
                DefaultValue = opts?.Amount,
            };
            yield return new SelectField
            {
                Name = nameof(DelayActionOptions.Unit).ToCamelCase(),
                Label = "Unit of Time",
                SelectFieldOptions = new SelectFieldOptionsBuilder
                {
                    { nameof(DelayActionOptions.UnitsOfTime.Days), "Days" },
                    { nameof(DelayActionOptions.UnitsOfTime.Hours), "Hours" },
                    { nameof(DelayActionOptions.UnitsOfTime.Minutes), "Minutes" },
                },
                IsRequired = true,
                DefaultValue = opts?.Unit,
            };
            yield return new SelectField
            {
                Name = nameof(DelayActionOptions.When).ToCamelCase(),
                Label = "When",
                SelectFieldOptions = new SelectFieldOptionsBuilder
                {
                    { nameof(DelayActionOptions.BeforeAfter.After), "After" },
                    { nameof(DelayActionOptions.BeforeAfter.Before), "Before" },
                },
                IsRequired = true,
                DefaultValue = opts?.When ?? DelayActionOptions.BeforeAfter.After,
                Visible = flowActionContext.ObjectType == nameof(Appointment) ?
                    null :
                    new[]
                    {
                        "false"
                    },
            };
            yield return new SelectField
            {
                Name = nameof(DelayActionOptions.Anchor).ToCamelCase(),
                Label = "Anchor Date",
                SelectFieldOptions = new SelectFieldOptionsBuilder
                {
                    { nameof(DelayActionOptions.Anchors.ExecutionTime), "Run Time" },
                    { nameof(DelayActionOptions.Anchors.Appointment), "Appointment" },
                },
                IsRequired = true,
                Visible = flowActionContext.ObjectType == nameof(Appointment) ?
                    new[] { nameof(DelayActionOptions.When).ToCamelCase() } :
                    new[]
                    {
                        "false"
                    },
                Enable = flowActionContext.ObjectType == nameof(Appointment) ? null : new[] { "false" },
                DefaultValue = opts?.Anchor ?? (
                    flowActionContext.ObjectType == nameof(Appointment) ?
                        DelayActionOptions.Anchors.Appointment :
                        DelayActionOptions.Anchors.ExecutionTime
                ),
            };
            yield return new CheckboxField
            {
                Name = nameof(DelayActionOptions.TruncateDate).ToCamelCase(),
                Label = "Date Only",
                DefaultValue = opts?.TruncateDate ?? false,
                Visible = new[]
                {
                    nameof(DelayActionOptions.Anchor).ToCamelCase()
                }
            };
            yield return new TextField
            {
                Name = nameof(DelayActionOptions.Tag).ToCamelCase(),
                Label = "Cancellation Tag",
                Visible = new[]
                {
                    nameof(DelayActionOptions.Anchor).ToCamelCase()
                },
                DefaultValue = opts?.Tag,
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, DelayActionOptions options)
    {
        // var eventType = await _connection.Filter<EventType>()
        //     .Eq(x => x.AccountId, context.AccountId.Value)
        //     .Eq(x => x.Id, step.EventIdTrigger)
        //     .Eq(x => x.ObjectType, flow.ObjectType)
        //     .FirstOrDefaultAsync();
        //
        // if (eventType == null) throw NotFoundException.New<EventType>(step.EventIdTrigger);

        step.Description = options.When switch
        {
            DelayActionOptions.BeforeAfter.Before => "Preparation",
            DelayActionOptions.BeforeAfter.After => "Delayed Execution",
            _ => null,
        };
        if (!string.IsNullOrWhiteSpace(options.Tag))
        {
            step.Description += $" ({options.Tag})";
        }

        var description = options.When switch
        {
            DelayActionOptions.BeforeAfter.Before => $"{options.Amount} {options.Unit} BEFORE ...",
            DelayActionOptions.BeforeAfter.After => $"{options.Amount} {options.Unit} AFTER ...",
            _ => null,
        };

        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Delayed/Offset", description, NextEventName);
        
        options.DelayedEventId = evt.Id;
        options.Output = new[]
        {
            output,
        };
    }
}