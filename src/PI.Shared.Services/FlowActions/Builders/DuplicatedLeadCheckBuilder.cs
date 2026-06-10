using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

public class DuplicatedLeadCheckBuilder : AbstractFlowActionBuilder<DuplicatedLeadCheckActionOptions, LeadDupeCheckAction.Message>
{
    public override Guid Id => ActionIds.DuplicatedLeadCheck;
    public override string Name => "Duplicate Lookup";
    public override string[] InputObjectTypes => new[]
    {
        nameof(Lead),
        "LMSTransaction"
    };

    public DuplicatedLeadCheckBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new LeadDupeCheckAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, DuplicatedLeadCheckActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield break;
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, DuplicatedLeadCheckActionOptions options)
    {
        var (evt1, out1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "New Lead", "New Lead is not a duplicate", NextEventName);
        var (evt2, out2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Lead is Duplicate", "New Lead is a duplicate", "duplicate");
        var (evt3, out3) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Duplicate lead created", "Duplicate lead created for Existing", "existing");
        step.Description = "Check whether New lead is a duplicate";
        options.NextEventId = evt1.Id;
        options.DuplicateLeadEventId = evt2.Id;
        options.OriginalLeadEventId = evt3.Id;
        // options.ErrorEventId
        options.Output = new[]
        {
            out1,
            out2,
            out3,
        };
    }
}