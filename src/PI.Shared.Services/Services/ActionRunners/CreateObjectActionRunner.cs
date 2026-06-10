using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Models;

namespace PI.Shared.Services.ActionRunners;

/// <summary>
/// First version of create object
/// </summary>
public class CreateObjectActionRunner(ILogger<CreateObjectActionRunner> logger, MongoConnection connection, ObjectTypeService objectTypeService)
    : AbstractObjectRunner<CreateObjectActionOptions>(logger, connection, objectTypeService)
{
    public override Guid ActionId => ActionIds.CreateObject;

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, CreateObjectActionOptions options)
    {
        var runContext = context.Run.BuildHandlebarsContext(context.Event);
        var fields = CalculateFields(context, runContext, options.Mapping);

        var targetObjectType = await _objectTypeService.GetAsync(context.EntityContext, options.ObjectType);
        if (targetObjectType == null) throw NotFoundException.New(options.ObjectType);

        var flowObjectType = default(string);
        if (fields.TryGetGuidParam(nameof(IFlowObject.FlowId), out var flowId))
        {
            var flow = await _connection.Filter<Flow>()
                .Eq(x => x.AccountId, context.EntityContext.AccountId.Value)
                .Eq(x => x.Id, flowId)
                .FirstOrDefaultAsync();

            flowObjectType = flow?.ObjectType;
        }

        var result = await _objectTypeService.AddObjectAsync(context.EntityContext, targetObjectType, fields, new ObjectTypeService.AddObjectOptions
        {
            PrepareEvent = (e) =>
            {
                e.Action ??= "ObjectCreated";

                if (!string.IsNullOrWhiteSpace(flowObjectType) && flowObjectType != targetObjectType.Name)
                {
                    _logger.LogInformation("Firing event for different {ObjectType}", flowObjectType);
                    e.ObjectType = flowObjectType;
                    e.Description ??= $"{flowObjectType} Created from {targetObjectType.Name}";
                    
                    e.AddRefValue(flowObjectType, context.Run.InitialObject[Model.IdFieldName]);
                }
                
                e.Description ??= $"{targetObjectType.Name} Created";

                e.AddRefValue(context.Run.ObjectType, context.Run.InitialObject[Model.IdFieldName]);
                e.AddRefValue(nameof(FlowRun), context.Run.Id);
            }
        });

        if (result)
        {
            _logger.LogInformation("{ObjectType} Created: {ObjectId}", targetObjectType.Name, result.Value.ObjectId);

            await _connection.Filter<FlowRun>()
                .Eq(x => x.AccountId, context.EntityContext.AccountId)
                .Eq(x => x.Id, context.Run.Id)
                .Update
                .Set(
                    x => x.Objects[FlowRun.GetObjectAlias(options.Alias ?? targetObjectType.FullName)],
                    new ObjectWithType
                    {
                        ObjectType = targetObjectType.FullName,
                        Object = await _objectTypeService.RecursivelyFlattenAsync(context.EntityContext, targetObjectType, result.Value.Object),
                    }
                )
                .UpdateOneAsync();
        }
        else
        {
            _logger.LogError("Failed to create {ObjectType}: {Status}", targetObjectType.Name, result.Status);
        }

        return getEvents().ToArray();

        IEnumerable<FlowEvent> getEvents()
        {
            if (result.IsSuccess)
            {
                var output = options.Output.FirstOrDefault(x => x.Name == CreateObjectActionOptions.ObjectCreatedEvent);
                if (output?.EventId.HasValue ?? false)
                {
                    var evt = new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.CreateObject),
                        Description = output.Description,
                        EventTypeId = output.EventId,
                    };
                    evt.AddRefValue(options.ObjectType, result.Value.ObjectId);
                    evt.SetMetaValue($"Action|Output|{options.ObjectType}Id", result.Value.ObjectId);

                    yield return evt;
                }
            }
            else
            {
                var output = options.Output.FirstOrDefault(x => x.Name == CreateObjectActionOptions.FailedToCreateObjectEvent);
                if (output?.EventId.HasValue ?? false)
                {
                    yield return new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.CreateObject),
                        Description = $"{output.Description}. {result.Status}",
                        EventTypeId = output.EventId,
                    };
                }
            }
        }
    }
}