using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace FlowActions;

public class ConditionalActionBuilder : AbstractFlowActionBuilder<ConditionalActionOptions, ConditionalAction.Message>
{
    private readonly ObjectTypeService _objectTypeService;
    public override string Name => "Conditional";

    public override Guid Id => ActionIds.Conditional;

    public override string[] InputObjectTypes => null;

    public ConditionalActionBuilder(MongoConnection connection, ObjectTypeService objectTypeService) : base(connection)
    {
        _objectTypeService = objectTypeService;
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new ConditionalAction.Message(evt, options);

    protected override async ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, ConditionalActionOptions opts = null)
    {
        var field = new ObjectField
        {
            Name = nameof(ConditionalActionOptions.Criteria).ToCamelCase(),
            Label = nameof(ConditionalActionOptions.Criteria),
            ObjectFieldOptions = new ObjectFieldOptions
            {
                ObjectType = nameof(Criteria),
            },
            DefaultValue = opts?.Criteria
        };

        var value = opts?.Criteria != null ? JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(opts.Criteria)) : null;

        await _objectTypeService.LoadObjectFieldAsync(flowActionContext.EntityContext, opts != null ? FormName.Edit : FormName.Add, field, value);
        
        return new FormField[]
        {
            field,
        };
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, ConditionalActionOptions options)
    {
        // TODO: use criteria to create "detailed" messages
        // ...

        var (evt1, out1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Conditions satisfied", "All conditions satisfied", "true");
        var (evt2, out2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Condition(s) not satisfied", "One or more conditions not satisfied", "false");
        step.Description = "Evaluate conditions";
        options.TrueEventId = evt1.Id;
        options.FalseEventId = evt2.Id;
        options.Output = new[]
        {
            out1,
            out2,
        };
    }
}