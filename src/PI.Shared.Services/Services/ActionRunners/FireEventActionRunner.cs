using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Requests;

namespace PI.Shared.Services.ActionRunners;

/// <summary>
/// Fire Event for object 
/// </summary>
public class FireEventActionRunner(ILogger<FireEventActionRunner> logger, MongoConnection connection, ObjectTypeService objectTypeService)
    : AbstractRunner<FireEventActionOptions>
{
    public override Guid ActionId => ActionIds.FireEvent;

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, FireEventActionOptions options)
    {
        var runContext = context.Run.BuildHandlebarsContext(context.Event);

        var resolveParameters = ExpressionEvaluatorService.TryResolveRecursively(context.EntityContext, runContext, options.Parameters);
        if (resolveParameters.IsError)
        {
            logger.LogError("Could not resolve object recursively: {Error}", resolveParameters.Status);
            return [];
        }

        var parameters = resolveParameters.Value;

        var targetObjectType = await objectTypeService.GetAsync(context.EntityContext, options.ObjectType);
        if (targetObjectType == null)
        {
            logger.LogError("{ObjectType}: Not found", options.ObjectType);
            throw NotFoundException.New(options.ObjectType);
        }

        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.UserId, out var userIdObj))
        {
            logger.LogError("Could not resolve {UserId}", options.UserId);
            return [];
        }

        var user = userIdObj.TryToParseObjectId(out var userId)
            ? await connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, context.EntityContext.AccountId.Value)
                .Eq(x => x.Id, userId)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync()
            : null;

        if (user == null)
        {
            logger.LogError("User not found {UserId}", userId);
            return [];
        }

        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.ObjectId, out var objectIdObj))
        {
            logger.LogError("Could not resolve {ObjectId}", options.ObjectId);
            return [];
        }
        
        if (!objectIdObj.TryToParseObjectId(out var objectId))
        {
            logger.LogError("Invalid {ObjectId}: {Type}", objectIdObj, objectIdObj?.GetType().FullName);
            return [];
        }

        var expandoObject = await objectTypeService.GetExpandoObjectByIdAsync(context.EntityContext, targetObjectType, objectId);
        if (expandoObject == null)
        {
            logger.LogError("{ObjectType} {ObjectId}: Not found", targetObjectType.FullName, options.ObjectId);
            return [];
        }

        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.Description, out var descriptuonObj) || descriptuonObj is not string description)
        {
            description = null;
        }

        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.Action, out var actionObj) || actionObj is not string action)
        {
            action = "Run";
        }

        var evt = UserActionService.BuildEvent(
            context.EntityContext,
            user,
            new DataFormActionRequest
            {
                Action = action,
                SelectedIds =
                [
                    objectId
                ],
                Parameters = parameters,
                View = null,
            },
            targetObjectType,
            expandoObject,
            options.EventTypeId.Value,
            description,
            Guid.CreateVersion7()
        );

        if (evt != null)
        {
            logger.LogInformation("Fire event for {ObjectType} {ObjectId}", targetObjectType.FullName, objectId);
            await objectTypeService.DispatchAsync(evt);
        }
        else
        {
            logger.LogInformation("Skip Event for {ObjectType} {ObjectId}", targetObjectType.FullName, objectId);
        }

        return [];
    }
}