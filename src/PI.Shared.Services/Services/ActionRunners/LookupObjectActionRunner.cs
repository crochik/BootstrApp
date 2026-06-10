using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Services.ActionRunners;

public class LookupObjectActionRunner(ILogger<LookupObjectActionRunner> logger, MongoConnection connection, ObjectTypeService objectTypeService)
    : AbstractRunner<LookupObjectActionOptions>
{
    public override Guid ActionId => ActionIds.LookupObject;

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, LookupObjectActionOptions options)
    {
        var runContext = context.Run.BuildHandlebarsContext(context.Event);
        var criteria = new List<Condition>();
        foreach (var condition in options.Criteria.Conditions)
        {
            if (condition.Value is not string criteriaValue)
            {
                criteria.Add(Condition.New(condition.FieldName, condition.Operator, condition.Value));
                continue;
            }

            if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, criteriaValue, out var value))
            {
                logger.LogError("Couldn't resolve {Expression} in Criteria", criteriaValue);
                throw new BadRequestException("Couldn't resolve expression");
            }

            criteria.Add(Condition.New(condition.FieldName, condition.Operator, value));
        }

        var objectType = await objectTypeService.GetAsync(context.EntityContext, options.ObjectType);
        if (objectType == null) throw NotFoundException.New(options.ObjectType);

        List<ExpandoObject> matches = null;
        if (criteria.FirstOrDefault(x => objectType.Fields.TryGetValue(x.FieldName, out var field) && field.Field is LocationField) != null)
        {
            // geo near query
            matches = await objectTypeService.FindNearAsync(context.EntityContext, objectType, criteria.ToArray(), 2);
        }
        else
        {
            // normal query
            matches = await objectTypeService.FindAsync(context.EntityContext, objectType, criteria.ToArray(), 2, options.OrderBy, options.ReverseOrder);
        }
        
        if (matches?.Count > 0)
        {
            logger.LogInformation("Found one or more matches for {ObjectType}: first is {ObjectId}", objectType.Name, matches[0].GetFieldValue("_id"));
            
            var result = await connection.Filter<FlowRun>()
                .Eq(x => x.AccountId, context.EntityContext.AccountId)
                .Eq(x => x.Id, context.Run.Id)
                .Update
                .Set(
                    x => x.Objects[FlowRun.GetObjectAlias(options.ObjectNickname ?? options.ObjectType)],
                    new ObjectWithType
                    {
                        ObjectType = options.ObjectType,
                        Object = await objectTypeService.RecursivelyFlattenAsync(context.EntityContext, objectType, matches[0]),
                    }
                )
                .UpdateOneAsync();

            if (result.MatchedCount != 1)
            {
                logger.LogError("Failed to update run");
            }
        }
        else
        {
            logger.LogInformation("Didn't find any matches {ObjectType}", objectType.Name);
        }

        return getEvents().ToArray();

        IEnumerable<FlowEvent> getEvents()
        {
            if (matches?.Count > 0)
            {
                // 1 or more
                var outputName = matches.Count == 1 ? LookupObjectActionOptions.ObjectFoundEvent : LookupObjectActionOptions.MoreThanOneObjectFoundEvent;
                var output = options.Output.FirstOrDefault(x => x.Name == outputName);
                if (output?.EventId.HasValue ?? false)
                {
                    var evt = new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.LookupObject),
                        Description = output.Description,
                        EventTypeId = output.EventId,
                    };
                    var id = matches[0].GetFieldValue(Model.IdFieldName);
                    evt.AddRefValue(objectType.Name, id);
                    evt.SetMetaValue($"Action|Output|{objectType.Name}Id", id);

                    yield return evt;
                }
            }
            else
            {
                // not found
                var output = options.Output.FirstOrDefault(x => x.Name == LookupObjectActionOptions.ObjectNotFoundEvent);
                if (output?.EventId.HasValue ?? false)
                {
                    var evt = new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.LookupObject),
                        Description = output.Description,
                        EventTypeId = output.EventId,
                    };

                    yield return evt;
                }
            }
        }
    }
}