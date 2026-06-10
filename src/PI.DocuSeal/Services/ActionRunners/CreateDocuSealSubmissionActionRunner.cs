using Crochik.Mongo;
using Messages.Flow;
using MongoDB.Bson;
using PI.DocuSeal.Models;
using PI.DocuSeal.Services;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Interfaces;
using PI.Shared.Services.ActionRunners;

namespace Services.ActionRunners;

public class CreateDucuSealSubmissionActionRunner(
    ILogger<CreateDucuSealSubmissionActionRunner> logger,
    MongoConnection connection,
    DocuSealService service
    )
    : AbstractRunner<CreateDocuSealSubmissionActionOptions>
{
    public override Guid ActionId => ActionIds.CreateDocuSealSubmission;

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, CreateDocuSealSubmissionActionOptions options)
    {
        var runContext = context.Run.BuildHandlebarsContext(context.Event);

        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.Name, out var resolved) || resolved is not string name)
        {
            logger.LogError("Could not resolve Name: {Expression}", options.Name);
            return buildErrorEvent($"Failed to resolve Name");
        }

        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.SubmitterName, out resolved) || resolved is not string submitterName)
        {
            logger.LogError("Could not resolve Submitter Name: {Expression}", options.SubmitterName);
            return buildErrorEvent($"Failed to resolve Submitter Name");
        }

        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.SubmitterEmail, out resolved) || resolved is not string submitterEmail)
        {
            logger.LogError("Could not resolve Submitter Email: {Expression}", options.SubmitterEmail);
            return buildErrorEvent($"Failed to resolve Submitter Email");
        }

        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.SubmitterRole, out resolved) || resolved is not string submitterRole)
        {
            logger.LogError("Could not resolve Submitter Role: {Expression}", options.SubmitterRole);
            return buildErrorEvent($"Failed to resolve Submitter Role");
        }

        string? objectType;
        Guid? objectId;
        if (options.ObjectType == null && options.ObjectId == null)
        {
            // the object for the flow
            objectType = context.ObjectType.FullName;
            objectId = context.ObjectId;
        }
        else
        {
            // objecttype
            if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.ObjectType, out resolved))
            {
                logger.LogError("Could not resolve Object Type: {Expression}", options.ObjectType);
                return buildErrorEvent($"Failed to resolve Object Type");
            }

            objectType = resolved as string;

            // object Id
            ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.ObjectId, out resolved);
            objectId = resolved.TryParseGuid(out var uuid) ? uuid : null;
        }

        // templateId
        ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.TemplateId, out resolved);
        if (!resolved.TryToParseObjectId(out var templateId))
        {
            logger.LogError("Could not resolve Template Id: {Expression}", options.TemplateId);
            return buildErrorEvent($"Failed to resolve Template Id");
        }

        var creatorId = options.CreatorId ?? "{{InitialEvent.Actor.UserId}}";
        ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, creatorId, out resolved);
        if (!resolved.TryToParseObjectId(out var userId))
        {
            logger.LogError("Could not resolve Creator Id: {Expression}", creatorId);
            return buildErrorEvent($"Failed to resolve Creator Id");
        }
        
        try
        {
            var user = await connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, context.EntityContext.AccountId)
                .Eq(x => x.Id, userId)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                logger.LogError("Invalid or Not found user: {UserId}", userId);
                return buildErrorEvent("Invalid or missing user");
            }
            
            var userContext = user.Context.WithActorFrom(context.EntityContext);
            
            if (options.ObjectType == null && options.ObjectId == null)
            {
                // TODO: should use runContext instead for the document generation
                // ....
            }

            var submission = new DocuSealSubmission
            {
                Name = name,
                TemplateId = templateId,
                Parent = !string.IsNullOrEmpty(objectType) && objectId.HasValue ? new ReferencedObject
                {
                    ObjectType = objectType,
                    ObjectId = objectId,
                } : null,
                Submitters =
                [
                    new DocuSealSubmitter
                    {
                        Name = submitterName,
                        Email = submitterEmail,
                        Role = submitterRole,
                    },
                ],
            };

            // copy from runContent into inputs
            var submissionContext = new Dictionary<string, object>();
            if (options.ObjectType == null && options.ObjectId == null)
            {
                if (runContext.TryResolveValue(["Object"], out var value))
                {
                    submissionContext["Object"] = value;
                }
            }
            
            if (options.Inputs != null)
            {
                foreach (var input in options.Inputs)
                {
                    if (ExpressionEvaluatorService.TryResolve(userContext, runContext, input.Value, out var value))
                    {
                        if (!submissionContext.SetFieldValue(input.Key.Split("|"), value))
                        {
                            logger.LogError("Failed to set input value: {Expression}", input.Key);
                            return buildErrorEvent($"Failed to set input value: {input.Key}");
                        }
                    }
                }
            }
            
            var result = await service.CreateSubmissionAsync(userContext, submission, submissionContext);
            if (result.IsError)
            {
                logger.LogError("Failed to create Submission: {Error}", result.Status);
                return buildErrorEvent(result.Status);
            }

            // TODO: could automatically load submission?
            // ...

            logger.LogInformation("Submission created: {SubmissionId}", result.Value.Id);

            var output = options.Output.FirstOrDefault(x => x.Name == CreateDocuSealSubmissionActionOptions.SuccessEventName);
            if (output?.EventId.HasValue ?? false)
            {
                var evt = new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.CreateDocuSealSubmission),
                    Description = output.Description,
                    EventTypeId = output.EventId,
                };
                evt.AddRefValue("docuseal|Submission", result.Value.Id);
                evt.SetMetaValue("Action|Output|docuseal|Submission|Id", result.Value.Id);

                return [evt];
            }

            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Submission");
            return buildErrorEvent(ex.Message);
        }

        FlowEvent[] buildErrorEvent(string message)
        {
            var output = options.Output.FirstOrDefault(x => x.Name == CreateDocuSealSubmissionActionOptions.FailedEvent);
            if (output?.EventId.HasValue ?? false)
            {
                return
                [
                    new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.CreateDocuSealSubmission),
                        Description = $"{output.Description}. {message}",
                        EventTypeId = output.EventId,
                    }
                ];
            }

            return [];
        }
    }
}