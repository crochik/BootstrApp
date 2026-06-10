using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

public class BootstrapProductCatalogActionBuilder : AbstractFlowActionBuilder<BootstrapProductCatalogActionOptions, BootstrapProductCatalogAction.Message>
{
    public override Guid Id => ActionIds.BootstrapProductCatalog;

    public override string Name => "Initialize Product Catalog";

    public override string[] InputObjectTypes => new[] {
        "ProductCatalog"
    };

    public BootstrapProductCatalogActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options) => new BootstrapProductCatalogAction.Message(evt, options);
    
    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, BootstrapProductCatalogActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            // no fields?
            yield break;
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, BootstrapProductCatalogActionOptions options)
    {
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Bootstrap Product Catalog", "Product Catalog bootstrapped", NextEventName);
        step.Description = "Bootstrap Product Catalog";
        options.NextEventId = evt.Id;
        // options.ErrorEventId
        options.Output = new []
        {
            output
        };
    }
}