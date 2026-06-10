using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Services.ActionRunners;

/// <summary>
/// Create object using form (fields)
/// </summary>
public class CreateObjectUsingFormActionRunner : AbstractRunner<CreateObjectUsingFormActionOptions>
{
    private readonly ILogger<CreateObjectUsingFormActionRunner> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    public override Guid ActionId => ActionIds.CreateObjectUsingForm;

    public CreateObjectUsingFormActionRunner(ILogger<CreateObjectUsingFormActionRunner> logger, MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, CreateObjectUsingFormActionOptions options)
    {
        var runContext = context.Run.BuildHandlebarsContext(context.Event);

        var resolvedFields = ExpressionEvaluatorService.TryResolveRecursively(context.EntityContext, runContext, options.Object); 
        if (resolvedFields.IsError)
        {
            _logger.LogError("Could not resolve object recursively: {Error}", resolvedFields.Status);
            var errorEvent = buildErrorEvent($"Failed to resolve object: {resolvedFields.Status}");
            return errorEvent == null ? [] : [errorEvent];
        }

        var fields = resolvedFields.Value;

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
                AllowInitialValueOverride = true,
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
            }
        );

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
                var output = options.Output.FirstOrDefault(x => x.Name == CreateObjectUsingFormActionOptions.ObjectCreatedEvent);
                if (output?.EventId.HasValue ?? false)
                {
                    var evt = new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.CreateObjectUsingForm),
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
                var errorEvent = buildErrorEvent(result.Status);
                if (errorEvent != null) yield return errorEvent;
            }
        }

        GenericFlowEvent buildErrorEvent(string message)
        {
            var output = options.Output.FirstOrDefault(x => x.Name == CreateObjectUsingFormActionOptions.FailToCreateObjectEvent);
            if (output?.EventId.HasValue ?? false)
            {
                return new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.CreateObjectUsingForm),
                    Description = $"{output.Description}. {message}",
                    EventTypeId = output.EventId,
                };
            }

            return null;
        }
    }
}