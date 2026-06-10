using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

public class AutoRefillBalanceActionBuilder : AbstractFlowActionBuilder<AutoRefillBalanceActionOptions, AutoRefillBalanceAction.Message>
{
    public override string Name => "Auto Refill Balance";
    public override string Description => "Auto Refill Balance for Organization";
    public override Guid Id => ActionIds.AutoRefillBalance;
    public override string IconName => null; // Id.ToString();
    public override string[] InputObjectTypes => new[] { nameof(PI.Shared.Models.Organization) };

    public AutoRefillBalanceActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new AutoRefillBalanceAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, AutoRefillBalanceActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            // no fields
            yield break;
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, AutoRefillBalanceActionOptions options)
    {
        var (evt1, out1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Balance Auto-refill", "Processed balance auto-refill", "refilled");
        var (evt2, out2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Auto-refill disabled", "Balance auto-refill is disabled", "disabled");
        var (evt3, out3) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "ErrorStartingCharge", "Failed to start charge", ErrorEventName);
        step.Description = "Auto Refill balance";
        options.RefilledEventId = evt1.Id;
        options.DisabledEventId = evt2.Id;
        options.ErrorEventId = evt3.Id;
        options.Output = new[]
        {
            out1,
            out2,
            out3,
        };
    }
}