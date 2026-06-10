using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

public class RunSingerSyncActionBuilder : AbstractFlowActionBuilder<RunSingerSyncActionOptions, RunSingerSyncAction.Message>
{
    public override string Name => "Run Singer Sync";
    public override Guid Id => ActionIds.RunSingerSync;
    public override string IconName => null; // Id.ToString();
    public override string[] InputObjectTypes => new[] { nameof(Account) };

    public RunSingerSyncActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new RunSingerSyncAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, RunSingerSyncActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield break;
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, RunSingerSyncActionOptions options)
    {
        // TODO: add configuration name? 
        // ...
        step.Description = "Run Singer Sync";
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Run Singer Sync", "Singer sync finished", NextEventName);
        options.SyncedEventId = evt.Id;
        options.Output = new[]
        {
            output,
        };
    }
}