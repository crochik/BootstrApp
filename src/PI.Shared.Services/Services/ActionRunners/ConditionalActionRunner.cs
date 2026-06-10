using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Services.ActionRunners;

public class ConditionalActionRunner : AbstractRunner<ConditionalActionOptions>
{
    private readonly ILogger<ConditionalActionRunner> _logger;
    private readonly ObjectTypeService _objectTypeService;
    public override Guid ActionId => ActionIds.Conditional;

    public ConditionalActionRunner(ILogger<ConditionalActionRunner> logger, ObjectTypeService objectTypeService)
    {
        _logger = logger;
        _objectTypeService = objectTypeService;
    }

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, ConditionalActionOptions options)
    {
        // -------
        // always update object in the run before evaluating 
        var updatedTargetObject = await _objectTypeService.GetFlatObjectAsync(context.EntityContext, context.ObjectType, context.ObjectId);
        if (updatedTargetObject == null) throw NotFoundException.New(context.ObjectType.Name, context.ObjectId);
        context.Run.Objects[FlowRun.GetObjectAlias(context.ObjectType.FullName)] = new ObjectWithType
        {
            ObjectType = context.ObjectType.FullName,
            Object = updatedTargetObject,
        };
        // -------
        
        var runContext = context.Run.BuildHandlebarsContext(context.Event);
        if (options.Criteria?.Conditions == null)
        {
            throw new BadRequestException("No conditions");
        }

        var match = true;
        foreach (var condition in options.Criteria.Conditions)
        {
            var c = condition;
            if (c.Value is string str && str.Contains("{{") && str.Contains("}}"))
            {
                if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, str, out var value))
                {
                    throw new BadRequestException($"Error evaluating condition value: {str}");
                }

                c = Condition.New(c.FieldName, c.Operator, value);
            }
            
            var fieldValue = runContext.ResolvePathValue(c.FieldName);
            var result = c.EvaluateValue(fieldValue);

            _logger.LogInformation("Condition: {FieldValue} {Operator} with {Value}: {Result}", c.FieldName, c.Operator, fieldValue, result);
            
            if (!result)
            {
                match = false;
                break;
            }
        }

        return getEvents().ToArray();

        IEnumerable<FlowEvent> getEvents()
        {
            if (match)
            {
                yield return new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.Conditional),
                    EventTypeId = options.TrueEventId,
                    Description = options.GetEventDescription(options.TrueEventId) ?? "Passed all conditions"
                };
            }
            else
            {
                yield return new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.Conditional),
                    EventTypeId = options.FalseEventId,
                    Description = options.GetEventDescription(options.FalseEventId) ?? "One or more conditions were not satisfied"
                };
            }
        }
    }
}