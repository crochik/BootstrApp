using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

public class TakeSnapshotActionBuilder : AbstractFlowActionBuilder<TakeSnapshotActionOptions, SimpleActionMessage<TakeSnapshotActionOptions>>
{
    public override Guid Id => ActionIds.TakeSnapshot;
    public override string Name => "Take Data Snapshot";

    // TODO: should allow objects that extend it as well
    // ...
    public override string[] InputObjectTypes => new[]
    {
        nameof(Snapshot),
        $"{nameof(Snapshot)}*"
    };

    public TakeSnapshotActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new SimpleActionMessage<TakeSnapshotActionOptions>(evt, options);
    
    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, TakeSnapshotActionOptions opts = null)
    {
        // no fields (YET)
        return ValueTask.FromResult(Enumerable.Empty<FormField>());
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, TakeSnapshotActionOptions options)
    {
        step.Description = $"Take Snapshot";
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Take Snapshot", "Snapshot taken", NextEventName);
        options.NextEventId = evt.Id;
        options.Output = new[]
        {
            output,
        };
    }
}