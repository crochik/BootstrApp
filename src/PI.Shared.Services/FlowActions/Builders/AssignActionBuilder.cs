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

public class AssignActionBuilder : LeadWithApptFlowActionBuilder<AssignActionOptions, AssignAction.Message>
{
    public override string Name => "Assign Object";
    public override string Description => "Assign Object to Entity";

    public override Guid Id => ActionIds.AssignLead;
    public override string[] InputObjectTypes => null;

    public AssignActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, AssignActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new ReferenceField
            {
                Name = nameof(AssignActionOptions.EntityId).ToCamelCase(),
                Label = "Assign Entity",
                IsRequired = true,
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = nameof(Entity),
                },
                DefaultValue = opts?.EntityId,
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, AssignActionOptions options)
    {
        string evtDescription;
        if (options.EntityId.HasValue)
        {
            var entity = await _connection.Filter<Entity>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, options.EntityId.Value)
                .FirstOrDefaultAsync();

            if (entity == null) throw NotFoundException.New<Entity>(options.EntityId.Value);
            step.Description = $"Assign {entity.Name} to {flow.ObjectType}";
            evtDescription = $"{entity.Name} assigned to {flow.ObjectType}";
        }
        else
        {
            step.Description = $"Unassign Entity from {flow.ObjectType}";
            evtDescription = $"Entity unassigned from {flow.ObjectType}";
        }

        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"(Un)Assign Entity", evtDescription, NextEventName);
        options.NextEventId = evt.Id;

        // options.ErrorEventId =

        options.Output = new[]
        {
            output,
        };
    }
}