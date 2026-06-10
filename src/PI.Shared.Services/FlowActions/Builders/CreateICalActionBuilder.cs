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

public class CreateICalActionBuilder : AbstractFlowActionBuilder<CreateICalActionOptions, SimpleActionMessage<CreateICalActionOptions>>
{
    public override Guid Id => ActionIds.CreateICal;
    public override string Name => "Create iCal file";
    public override string Description => "Generate iCal file for appointment";
    public override string[] InputObjectTypes => new[] { nameof(Appointment) };
    
    public CreateICalActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new SimpleActionMessage<CreateICalActionOptions>(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, CreateICalActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new TextField
            {
                Name = nameof(CreateICalActionOptions.Summary).ToCamelCase(),
                Label = "Summary (template)",
                IsRequired = true,
                DefaultValue = opts?.Summary,
            };

            yield return new TextField
            {
                Name = nameof(CreateICalActionOptions.Description).ToCamelCase(),
                Label = "Description (template)",
                IsRequired = false,
                TextFieldOptions = new TextFieldOptions
                {
                    Multline = true,
                },
                DefaultValue = opts?.Description,
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, CreateICalActionOptions options)
    {
        step.Description = "Create iCal file for appointment";
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Create iCal file for appointment", "iCal file created for appointment", NextEventName);
        options.NextEventId = evt.Id;
        options.Output = new[]
        {
            output
        };
    }
}