using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Messages.Flow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;
using PI.Shared.Services.ActionRunners;

namespace Services.ActionRunners;

public class CreateSalesforceObjectActionRunner(
    ILogger<CreateSalesforceObjectActionRunner> logger,
    ObjectTypeService objectTypeService,
    SalesforceService salesforceService,
    IServiceProvider serviceProvider
    ) : AbstractRunner<CreateSalesforceObjectActionOptions>
{
    public override Guid ActionId => ActionIds.CreateSalesforceObject;

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, CreateSalesforceObjectActionOptions options)
    {
        var runContext = context.Run.BuildHandlebarsContext(context.Event);

        var resolvedFields = ExpressionEvaluatorService.TryResolveRecursively(context.EntityContext, runContext, options.Mapping);
        if (resolvedFields.IsError)
        {
            logger.LogError("Could not resolve object recursively: {Error}", resolvedFields.Status);
            return buildErrorEvent($"Failed to resolve object: {resolvedFields.Status}");
        }

        var accountContext = new AccountContext(context.EntityContext.AccountId.Value);
        var token = await salesforceService.GetTokenAsync(accountContext, GetTokenOptions.Default);
        if (token.IsError)
        {
            logger.LogError("Failed to get Token: {Error}", token.Status);
            return buildErrorEvent(token.Status);
        }

        var fields = resolvedFields.Value;

        try
        {
            var result = await salesforceService.SalesforceClient.CreateAsync(token.Value, options.ObjectType, fields);
            if (result.error != null)
            {
                logger.LogError("Failed to create {ObjectType}: {Error}", options.ObjectType, result.error);
                return buildErrorEvent(result.error);
            }

            // auto import 
            // ...
            if (options.ObjectType switch
                {
                    // "Lead" => true,
                    // "Account" => true,
                    // "ServiceAppointment" => true,
                    "WorkOrder" => true,
                    _ => false,
                })
            {
                await ImportObjectAsync(context, options, result.id);
            }
            
            var output = options.Output.FirstOrDefault(x => x.Name == CreateSalesforceObjectActionOptions.ObjectCreatedEvent);
            if (output?.EventId.HasValue ?? false)
            {
                var evt = new GenericFlowEvent(context.Event)
                {
                    Action = nameof(ActionIds.CreateSalesforceObject),
                    Description = output.Description,
                    EventTypeId = output.EventId,
                };
                evt.AddRefValue(options.ObjectType, result.id);
                evt.SetMetaValue($"Action|Output|{options.ObjectType}|ExternalId", result.id);

                return [evt];
            }

            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create {ObjectType}", options.ObjectType);
            return buildErrorEvent(ex.Message);
        }

        FlowEvent[] buildErrorEvent(string message)
        {
            var output = options.Output.FirstOrDefault(x => x.Name == CreateSalesforceObjectActionOptions.FailedToCreateObjectEvent);
            if (output?.EventId.HasValue ?? false)
            {
                return
                    [
                        new GenericFlowEvent(context.Event)
                        {
                            Action = nameof(ActionIds.CreateSalesforceObject),
                            Description = $"{output.Description}. {message}",
                            EventTypeId = output.EventId,
                        }
                    ];
            }

            return [];
        }
    }

    private async ValueTask<IFlowObject> ImportObjectAsync(ActionRunnerContext context, CreateSalesforceObjectActionOptions options, string sfId)
    {
        logger.LogInformation("Import WorkOrder: {SfId}", sfId);
        
        var objectType = await objectTypeService.GetAsync<SalesforceObjectType>(context.EntityContext, "sf_WorkOrder");
                
        using IServiceScope serviceScope = serviceProvider.CreateScope();
        var processor = serviceScope.ServiceProvider.GetRequiredService<IOnWorkOrderChangeProcessor>();
        var sfObject = await salesforceService.LoadObjectAsync<SfWorkOrderObject>(context.EntityContext, objectType, sfId);
        if (sfObject != null)
        {
            logger.LogInformation("{SfObjectType} loaded: {ObjectId}", options.ObjectType, sfObject.Id);
            var imported = await processor.ImportObjectAsync(context.EntityContext, objectType, sfObject);

            if (imported.FlowId.HasValue)
            {
                logger.LogInformation("Fire Update Event for loaded {ObjectId}", imported.Id);
                await objectTypeService.FireObjectUpdatedAsync(context.EntityContext, imported, new Dictionary<string, object>
                {
                    { nameof(SalesforceCustomObject.Properties), "*" },
                }, e =>
                {
                    e.Description = "Loaded Project (WorkOrder)";
                });
            }
        }
        
        return sfObject;
    }
}