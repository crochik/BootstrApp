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

public class TagObjectActionRunner : AbstractObjectRunner<TagObjectActionOptions>
{
    public override Guid ActionId => ActionIds.TagObject;

    public TagObjectActionRunner(ILogger<TagObjectActionRunner> logger, MongoConnection connection, ObjectTypeService objectTypeService)
        : base(logger, connection, objectTypeService)
    {
    }

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, TagObjectActionOptions options)
    {
        var runContext = context.Run.BuildHandlebarsContext(context.Event);

        var objectType = context.ObjectType;
        var targetObjectTypeName = objectType.Name;

        if (!string.IsNullOrEmpty(options.ObjectPath))
        {
            var parts = options.ObjectPath.Split(".");
            if (parts.Length != 2) throw new BadRequestException("Invalid Object Path");
            if (parts[0] != "Objects") throw new BadRequestException("Only Objects.* supported");
            // ...
            // objectType = 
            // targetObjectTypeName = 

            throw new BadRequestException("Not implemented yet");
        }

        if (!TryGetGuid(context, runContext, "{{Objects." + targetObjectTypeName + "._id}}", out var targetObjectId))
        {
            throw new BadRequestException($"Couldn't find object id: {targetObjectTypeName}");
        }

        var result = await TagObjectAsync(context, runContext, objectType, targetObjectId, options);

        if (result.IsSuccess)
        {
            _logger.LogInformation("{ObjectType} Tagged: {ObjectId}", targetObjectTypeName, targetObjectId);
            
            // TODO: add ref to run?
            // await _connection.Filter<FlowRun>()
            //     .Eq(x=>x.Id, context.Run.Id)
            //     .Update
            //     .Set(x=>x.Refs)
        }
        else if (result.IsError)
        {
            _logger.LogError("Failed to tag {ObjectType} {ObjectId}: {Status}", targetObjectTypeName, targetObjectId, result.Status);
        }
        else
        {
            _logger.LogInformation("Did not tag {ObjectType} {ObjectId}: {Status}", targetObjectTypeName, targetObjectId, result.Status);
        }

        return getEvents().ToArray();

        IEnumerable<FlowEvent> getEvents()
        {
            if (result.IsSuccess)
            {
                yield return new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.TagObject),
                    Description = options.GetEventDescription(options.NextEventId) ?? $"{context.ObjectType.Description ?? context.ObjectType.Name} Tagged",
                    EventTypeId = options.NextEventId,
                };
                yield break;
            }

            // error or unknown 
            
            if (result.IsUnknown && options.AlreadyTaggedEventId.HasValue)
            {
                // TODO: the name is actually misleading, it will fire when couldn't resolve the tag as well
                // ...
                yield return new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.TagObject),
                    Description = options.GetEventDescription(options.AlreadyTaggedEventId, result.Status),
                    EventTypeId = options.AlreadyTaggedEventId,
                };
            }

            if (options.AlwaysFireNextEvent)
            {
                yield return new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.TagObject),
                    Description = $"{result.Status}, ignore and continue...",
                    EventTypeId = options.NextEventId,
                };
            }
        }
    }

    private async Task<Result<ExpandoObject>> TagObjectAsync(ActionRunnerContext context, ExpandoObject runContext, ObjectType objectType, Guid objectId, TagObjectActionOptions options)
    {
        var obj = await _objectTypeService.GetExpandoObjectByIdAsync(context.EntityContext, objectType, objectId);
        if (obj == null) throw new NotFoundException(objectType.Name, objectId);

        var fieldName = options.FieldName ?? "Tags";
        if (!objectType.Fields.TryGetValue(fieldName, out var field)) throw new NotFoundException($"{fieldName} not found");
        switch (field.Field)
        {
            case CheckboxField:
            {
                if (obj.TryGetFieldValue(field.Field.Name, out var value))
                {
                    var set = value switch
                    {
                        bool bit => bit,
                        string str => bool.TryParse(str, out var bit) && bit,
                        null => false,
                        _ => throw new Exception($"Unexpected field value for checkbox: {value.GetType().FullName}")
                    };

                    if (set)
                    {
                        _logger.LogInformation("Checkbox is already checked");
                        return Result.Unknown<ExpandoObject>("Checkbox field is already true");
                    }
                }

                _logger.LogInformation("set field value to true");

                obj = await _objectTypeService.UpdateObjectAsync(
                    context.EntityContext,
                    objectType,
                    objectId,
                    q => q
                        .Ne(field.Field.GetPathInCollection(), true)
                        .Update
                        .Set(field.Field.GetPathInCollection(), true),
                    new Dictionary<string, object>
                    {
                        { field.Field.Name, true }
                    }
                );

                break;
            }

            case TagsField:
            {
                if (!TryGet(context, runContext, options.Tag, out string tag))
                {
                    return Result.Error<ExpandoObject>("Couldn't resolve tag");
                }

                if (string.IsNullOrWhiteSpace(tag))
                {
                    return Result.Unknown<ExpandoObject>("Tag without value");
                }

                if (obj.TryGetFieldValue(field.Field.Name, out var value) && value != null)
                {
                    var tags = value switch
                    {
                        string[] objs => objs,
                        IEnumerable<string> objs => objs.ToArray(),
                        IEnumerable<object> objs => objs.OfType<string>().ToArray(),
                        _ => throw new Exception($"Unexpected field value for tags: {value.GetType().FullName}")
                    };

                    if (tags.Any(x => string.Equals(x, tag)))
                    {
                        _logger.LogInformation("Object already tagged");
                        return Result.Unknown<ExpandoObject>("Object already tagged");
                    }
                }

                _logger.LogInformation("Add tag to field");

                obj = await _objectTypeService.UpdateObjectAsync(
                    context.EntityContext,
                    objectType,
                    objectId,
                    q => q
                        .Ne(field.Field.GetPathInCollection(), tag)
                        .Update
                        .AddToSet(field.Field.GetPathInCollection(), tag),
                    new Dictionary<string, object>
                    {
                        { field.Field.Name, "[...]" }
                    }
                );
                break;
            }

            default:
                throw new BadRequestException("Invalid field type");
        }

        if (obj == null) return Result.Unknown<ExpandoObject>("Update failed");

        return Result.Success(obj);
    }
}