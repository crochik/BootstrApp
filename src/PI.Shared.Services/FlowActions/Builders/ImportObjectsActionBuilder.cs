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

public class ImportObjectsActionBuilder : AbstractFlowActionBuilder<ImportObjectsActionOptions, SimpleActionMessage<ImportObjectsActionOptions>>
{
    public override Guid Id => ActionIds.ImportObjects;
    public override string Name => "Import Objects";

    public override string[] InputObjectTypes => new[]
    {
        nameof(ImportObjectsJob)
    };
    
    public ImportObjectsActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new SimpleActionMessage<ImportObjectsActionOptions>(evt, options);
    
    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, ImportObjectsActionOptions opts = null)
    {
        // no fields (YET)
        return ValueTask.FromResult(Enumerable.Empty<FormField>());
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, ImportObjectsActionOptions options)
    {
        step.Description = $"Import Objects";
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "ObjectsImported", "Objects Imported", NextEventName);
        options.NextEventId = evt.Id;
        options.Output = new[]
        {
            output,
        };
    }
}