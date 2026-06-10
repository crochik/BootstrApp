using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

/// <summary>
/// Not used/implemented ?????
/// </summary>
public class SyncCatalogFeedActionBuilder : AbstractFlowActionBuilder<SyncCatalogFeedActionOptions, SyncCatalogFeedAction.Message>
{
    public override string Name => "Run Catalog Feed Sync";
    public override Guid Id => ActionIds.RunCatalogFeedSync;
    public override string IconName => null; // Id.ToString();
    public override string[] InputObjectTypes => new[] { "CatalogFeed" };

    public SyncCatalogFeedActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new RunSingerSyncAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, SyncCatalogFeedActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield break;
        }
    }

    protected override Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, SyncCatalogFeedActionOptions options)
    {
        step.Description = "Sync Catalog Feed";
        return Task.CompletedTask;
    }
}