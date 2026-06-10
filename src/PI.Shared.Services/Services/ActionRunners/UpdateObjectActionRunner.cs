using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.Shared.Constants;
using PI.Shared.Exceptions;

namespace PI.Shared.Services.ActionRunners;

public class UpdateObjectActionRunner : AbstractObjectRunner<UpdateObjectActionOptions>
{
    public override Guid ActionId => ActionIds.UpdateObject;

    public UpdateObjectActionRunner(ILogger<UpdateObjectActionRunner> logger, MongoConnection connection, ObjectTypeService objectTypeService)
        : base(logger, connection, objectTypeService)
    {
    }

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, UpdateObjectActionOptions options)
    {
        var runContext = context.Run.BuildHandlebarsContext(context.Event);

        var targetObjectType = context.ObjectType;

        if (!string.IsNullOrWhiteSpace(options.ObjectType) && options.ObjectType != context.ObjectType.Name)
        {
            targetObjectType = await _objectTypeService.GetAsync(context.EntityContext, options.ObjectType);

            if (targetObjectType == null)
            {
                throw new BadRequestException("Failed to load object type");
            }
        }

        var targetObjectId = context.ObjectId;

        if (!string.IsNullOrEmpty(options.ObjectId))
        {
            var targetObjectTypeName = options.ObjectType;

            if (!TryGet(context, runContext, options.ObjectId, out var targetObject)) // e.g. "{{Objects.XXXXX._id}}"
            {
                throw new BadRequestException($"Couldn't find object id: {targetObjectTypeName}");
            }

            if (!targetObject.TryToParseObjectId(out var objectId)) throw new BadHttpRequestException("Unexpected Id type");
            targetObjectId = objectId;
        }

        var updates = CalculateFields(context, runContext, options.Mapping);
        
        var expando = await _objectTypeService.GetExpandoObjectByIdAsync(context.EntityContext, targetObjectType, targetObjectId);

        // TODO: could add code to explicitly handle the object status change as well
        // one way would be to remove from updates and then make an explicit call to the objecttypeservice setstatus 
        // all so we would fire the statusentered event :)
        // ... 

        var result = await _objectTypeService.UpdateObjectAsync(context.EntityContext, targetObjectType, updates, targetObjectId, expando, new ObjectTypeService.UpdateObjectOptions
        {
            PartialUpdate = true,
        });

        if (result)
        {
            if (result.Value.Skipped)
            {
                _logger.LogInformation("{ObjectType} {ObjectId}: Update skipped, no changes detected", targetObjectType.Name, targetObjectId);
            }
            else
            {
                var modifiedFields = string.Join(",", result.Value.UpdatedFields.Keys);
                _logger.LogInformation("{ObjectType} Updated: {ObjectId}: {ModifiedFields}", targetObjectType.Name, targetObjectId, modifiedFields);    
            }

            // TODO: add ref to run?
            // await _connection.Filter<FlowRun>()
            //     .Eq(x=>x.Id, context.Run.Id)
            //     .Update
            //     .Set(x=>x.Refs)
        }
        else
        {
            _logger.LogError("Failed to update {ObjectType} {ObjectId}: {Status}", targetObjectType.Name, targetObjectId, result.Status);
        }

        return getEvents().ToArray();

        IEnumerable<FlowEvent> getEvents()
        {
            if (result.IsSuccess)
            {
                _logger.LogInformation("Updated {ObjectType} {ObjectId}", targetObjectType.Name, targetObjectId);

                var output = options.Output.FirstOrDefault(x => x.Name == UpdateObjectActionOptions.ObjectUpdatedEvent);
                if (output?.EventId.HasValue ?? false)
                {
                    var evt = new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.UpdateObject),
                        Description = output.Description,
                        EventTypeId = output.EventId,
                    };

                    evt.AddRefValue(targetObjectType.Name, targetObjectId);
                    evt.SetMetaValue($"Action|Output|{targetObjectType.Name}Id", targetObjectId);

                    yield return evt;
                }
            }
            else
            {
                _logger.LogError("Failed to update {ObjectType} {ObjectId}: {Status}", targetObjectType.Name, targetObjectId, result.Status);

                var output = options.Output.FirstOrDefault(x => x.Name == UpdateObjectActionOptions.FailedToUpdateObjectEvent);
                if (output?.EventId.HasValue ?? false)
                {
                    yield return new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.UpdateObject),
                        Description = $"{output.Description}. {result.Status}",
                        EventTypeId = output.EventId,
                    };
                }
            }
        }
    }
}