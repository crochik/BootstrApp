using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

public class GetUserInputActionBuilder : AbstractFlowActionBuilder<GetUserInputActionOptions, GetUserInputAction.Message>
{
    public override string Name => "Get User Input";

    public override Guid Id => ActionIds.GetUserInput;
    public override string IconName => Id.ToString();

    public override string[] InputObjectTypes => null;

    public GetUserInputActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new GetUserInputAction.Message(evt, options);

    protected override async ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, GetUserInputActionOptions opts = null)
    {
        var eventTypes = await _connection.Filter<EventType>()
            .Eq(x => x.AccountId, flowActionContext.EntityContext.AccountId.Value)
            .Eq(x => x.ObjectType, flowActionContext.ObjectType)
            .Eq($"{nameof(EventType.Trigger)}._t", nameof(TriggerType.User))
            .SortAsc(x => x.Name)
            .FindAsync();
        
        return getFields();

        IEnumerable<FormField> getFields()
        {
            yield return new SelectField
            {
                Name = nameof(GetUserInputActionOptions.NextEventId).ToCamelCase(),
                Label = "User Action",
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = eventTypes.ToDictionary(x => x.Id.ToString(), x => x.Name)
                },
                DefaultValue = opts?.NextEventId,
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, GetUserInputActionOptions options)
    {
        step.Description = "Get User input";
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Get User Input", "Received user input", NextEventName);
        options.NextEventId = evt.Id;
        options.Output = new[]
        {
            output,
        };
    }
}