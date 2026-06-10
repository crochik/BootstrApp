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

public class SwitchActionRunner : AbstractRunner<SwitchActionOptions>
{
    private readonly ILogger<SwitchActionRunner> _logger;
    private readonly ObjectTypeService _objectTypeService;
    public override Guid ActionId => ActionIds.Switch;

    public SwitchActionRunner(ILogger<SwitchActionRunner> logger, ObjectTypeService objectTypeService)
    {
        _logger = logger;
        _objectTypeService = objectTypeService;
    }

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, SwitchActionOptions options)
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
        var eventId = options.DefaultEventId;
        var eventName = options.DefaultCase;
        foreach (var switchCase in options.Cases)
        {
            _logger.LogInformation("Evaluate {Case}", switchCase.Name);
            if (evaluate(switchCase))
            {
                _logger.LogInformation("{Case}: Match", switchCase.Name);
                eventId = switchCase.EventId;
                eventName = switchCase.Name;
                break;
            }
        }

        if (eventId == options.DefaultEventId)
        {
            _logger.LogInformation("Fallthrough to Default");
        }

        return getEvents().ToArray();

        bool evaluate(SwitchCase switchCase)
        {
            if (switchCase.Conditions == null)
            {
                throw new BadRequestException("No conditions");
            }

            foreach (var condition in switchCase.Conditions)
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

                object fieldValue;
                if (c.FieldName.Contains(' ') || c.FieldName.Contains('?') || c.FieldName.Contains('!'))
                {
                    // should be in all cases but since the previous implementation (ResolvePathValue) would return null if the 
                    // path was not found and ExpressionEvaluatorService will return the actual Path string
                    // ONLY USING ExpressionEvaluatorService if we know for sure that it is a (new) expression 
                    if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, c.FieldName, out fieldValue))
                    {
                        throw new BadRequestException($"Error evaluating field value: {c.FieldName}");
                    }
                }
                else
                {
                    // TODO: get rid of this ... eventually ... 
                    // as now it will break existing flows that assume not finding a path will return NULL and
                    // some use the Field Path (e.g. w/o {{}} and with | as separator)
                    fieldValue = runContext.ResolvePathValue(c.FieldName);
                }
                
                var result = c.EvaluateValue(fieldValue);

                _logger.LogInformation("Condition: {FieldValue} {Operator} with {Value}: {Result}", c.FieldName, c.Operator, fieldValue, result);

                if (!result)
                {
                    return false;
                }
            }

            return true;
        }

        IEnumerable<FlowEvent> getEvents()
        {
            yield return new GenericFlowEvent(context.Event)
            {
                Action = nameof(ActionIds.Switch),
                EventTypeId = eventId,
                Description = eventName
            };
        }
    }
}