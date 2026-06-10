using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;

namespace FlowActions;

public class StartFlowActionBuilder : AbstractFlowActionBuilder<StartFlowActionOptions, SimpleActionMessage<StartFlowActionOptions>>
{
    public override Guid Id => ActionIds.StartFlow;
    public override string Name => "Initiate Flow";
    public override string[] InputObjectTypes => null;
    
    public StartFlowActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        if (options is not StartFlowActionOptions opts)
        {
            throw new BadRequestException("Invalid options for action");
        }

        return new SimpleActionMessage<StartFlowActionOptions>(evt, opts);
    }

    protected override async ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, StartFlowActionOptions opts = null)
    {
        var objectType = await ObjectTypeService.GetAsync(_connection, flowActionContext.EntityContext, flowActionContext.ObjectType);
        if (objectType == null)
        {
            throw new NotFoundException($"{flowActionContext.ObjectType} not found");
        }

        var allFields = objectType.Fields.Values.Select(x => x.Field).ToArray();
        var fields = Enumerable.Empty<FormField>()
                .Concat(allFields.OfType<ReferenceField>().Where(x=>x.ReferenceFieldOptions.ObjectType==nameof(Flow)))
                .Concat(allFields.OfType<MultiReferenceField>().Where(x=>x.MultiReferenceFieldOptions.ObjectType==nameof(Flow)))
                .ToDictionary(x=>x.Name, x=>x.Label ?? x.Name)
            ;
        
        return getFields();

        IEnumerable<FormField> getFields()
        {
            yield return new SelectField
            {
                Name = nameof(StartFlowActionOptions.FieldName).ToCamelCase(),
                Label = "Field",
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = fields
                },
                DefaultValue = opts?.FieldName,
            };

            yield return new ReferenceField
            {
                Name = nameof(StartFlowActionOptions.FlowId).ToCamelCase(),
                Label = "Flow",
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = nameof(Flow),
                    Criteria = new []
                    {
                        Condition.Eq(nameof(Flow.ObjectType), objectType.Name), 
                    }
                },
                DefaultValue = opts?.FlowId,
            };            
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, StartFlowActionOptions options)
    {
        var otherFlow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, options.FlowId)
            .Eq(x => x.ObjectType, flow.ObjectType)
            .FirstOrDefaultAsync();

        if (otherFlow == null) throw NotFoundException.New<Flow>(options.FlowId);

        step.Description = $"'{options.FieldName}': Initiate '{otherFlow.Name}'";
        
        var (evt1, output1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"Initiated flow '{otherFlow.Name}'", $"'{otherFlow.Name}' Initiated for '{options.FieldName}'", NextEventName);
        var (evt2, output2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"Flow '{otherFlow.Name}' already set", $"'{otherFlow.Name}' already running", "alreadyRunning");
        options.NextEventId = evt1.Id;
        options.AlreadyRunningEventId = evt2.Id;
        options.Output = new[]
        {
            output1,
            output2,
        };
    }
}