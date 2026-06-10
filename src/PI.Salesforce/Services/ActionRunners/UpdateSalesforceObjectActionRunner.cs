using System;
using System.Linq;
using System.Threading.Tasks;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;
using PI.Shared.Services.ActionRunners;

namespace Services.ActionRunners;

public class UpdateSalesforceObjectActionRunner(ILogger<UpdateSalesforceObjectActionRunner> logger, SalesforceService salesforceService)
    : AbstractRunner<UpdateSalesforceObjectActionOptions>
{
    public override Guid ActionId => ActionIds.UpdateSalesforceObject;

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, UpdateSalesforceObjectActionOptions options)
    {
        var runContext = context.Run.BuildHandlebarsContext(context.Event);

        var resolvedFields = ExpressionEvaluatorService.TryResolveRecursively(context.EntityContext, runContext, options.Mapping);
        if (resolvedFields.IsError)
        {
            logger.LogError("Could not resolve object recursively: {Error}", resolvedFields.Status);
            var errorEvent = buildErrorEvent($"Failed to resolve object: {resolvedFields.Status}");
            return errorEvent == null ? [] : [errorEvent];
        }

        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.ObjectId, out var objectIdObj) || objectIdObj is not string objectId )
        {
            logger.LogError("Could not resolve objectId: {Expression} {Error}", options.ObjectId, resolvedFields.Status);
            var errorEvent = buildErrorEvent($"Failed to resolve object: {resolvedFields.Status}");
            return errorEvent == null ? [] : [errorEvent];
        }

        var accountContext = new AccountContext(context.EntityContext.AccountId.Value);
        var token = await salesforceService.GetTokenAsync(accountContext, GetTokenOptions.Default);
        if (token.IsError)
        {
            logger.LogError("Failed to get Token: {Error}", token.Status);
            var errorEvent = buildErrorEvent(token.Status);
            return [errorEvent];
        }

        var fields = resolvedFields.Value;

        try
        {
            await salesforceService.SalesforceClient.UpdateAsync(token.Value, options.ObjectType, objectId, fields);
            
            var output = options.Output.FirstOrDefault(x => x.Name == UpdateSalesforceObjectActionOptions.ObjectUpdatedEvent);
            if (output?.EventId.HasValue ?? false)
            {
                var evt = new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.UpdateSalesforceObject),
                    Description = output.Description,
                    EventTypeId = output.EventId,
                };
                evt.AddRefValue(options.ObjectType, objectId);
                evt.SetMetaValue($"Action|Output|{options.ObjectType}|ExternalId", objectId);

                return [evt];
            }

            return [];            
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update {ObjectType} {ObjectId}", options.ObjectType, objectId);
            var errorEvent = buildErrorEvent(ex.Message);
            return [errorEvent];
        }

        GenericFlowEvent buildErrorEvent(string message)
        {
            var output = options.Output.FirstOrDefault(x => x.Name == UpdateSalesforceObjectActionOptions.FailedToUpdateObjectEvent);
            if (output?.EventId.HasValue ?? false)
            {
                return new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.UpdateSalesforceObject),
                    Description = $"{output.Description}. {message}",
                    EventTypeId = output.EventId,
                };
            }

            return null;
        }
    }
}