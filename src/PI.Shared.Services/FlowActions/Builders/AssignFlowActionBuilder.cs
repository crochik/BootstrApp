using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

[Obsolete]
public class AssignFlowActionBuilder : LeadWithApptFlowActionBuilder<AssignFlowActionOptions, AssignFlowAction.Message>
{
    public override string Name => "Assign Flow";
    public override string Description => "Assign Flow to Lead";
    public override Guid Id => ActionIds.AssignFlow;
    public override string[] InputObjectTypes => new[] { nameof(PI.Shared.Models.Lead) };

    public AssignFlowActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, AssignFlowActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new TextField
            {
                Name = nameof(AssignFlowActionOptions.Tag).ToCamelCase(),
                Label = "Tag",
                IsRequired = true,
                DefaultValue = opts?.Tag,
            };

            yield return new ReferenceField
            {
                Name = nameof(AssignFlowActionOptions.FallbackFlowId).ToCamelCase(),
                Label = "Fallback Flow",
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = nameof(Flow),
                },
                DefaultValue = opts?.FallbackFlowId,
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, AssignFlowActionOptions options)
    {
        if (string.IsNullOrEmpty(options.Tag))
        {
            throw new BadRequestException("Missing required Tag");
        }

        if (!options.FallbackFlowId.HasValue)
        {
            throw new BadRequestException("Missing required Fallback Flow Id");
        }

        var fallback = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, options.FallbackFlowId.Value)
            .FirstOrDefaultAsync();

        if (fallback == null) throw NotFoundException.New<Flow>(options.FallbackFlowId.Value);

        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Apply Flow Transition", $"Flow transition `{options.Tag}` applied", NextEventName);
        
        step.Description = $"Apply flow transition `{options.Tag}`";
        options.NextEventId = evt.Id;
        // options.ErrorEventId
        options.Output = new[]
        {
            output,
        };
    }
}