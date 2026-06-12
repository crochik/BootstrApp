using Crochik.Logging;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Integrations.Delivery;
using PI.Shared.Integrations.Subscriptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;
using PI.Shared.Services.ActionRunners;

namespace PI.Shared.Integrations.ActionRunners;

public class FireWebhookActionRunner(
    ILogger<FireWebhookActionRunner> logger,
    ObjectTypeService objectTypeService,
    ISubscriptionStore store,
    IEventPublisher publisher
) : AbstractRunner<FireWebhookActionOptions>
{
    public override Guid ActionId => ActionIds.FireWebhook;

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, FireWebhookActionOptions options)
    {
        try
        {
            await FireWebhookAsync(context, options);
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process webhook");

            // TODO: return error event?
            // ...
            return [];
        }
    }

    private async Task<bool> FireWebhookAsync(ActionRunnerContext context, FireWebhookActionOptions options)
    {
        var evt = context.Event;

        using var scope = logger.AddScope(new
        {
            evt.ObjectType,
            evt.TargetId,
        });

        logger.LogInformation("Fire Webhook action");

        var eventKey = options.EventId;

        var runContext = context.Run.BuildHandlebarsContext(evt);
        if (!ExpressionEvaluatorService.TryResolve(context.EntityContext, runContext, options.OrganizationId, out var organizationIdObj))
        {
            logger.LogError("Failed to resolve organization id {OrganizationId}", options.OrganizationId);
            return false;
        }

        var organizationId = organizationIdObj switch
        {
            null => default(Guid?),
            string str => Guid.TryParse(str, out var uuid) ? uuid : Guid.Empty,
            Guid guid => guid,
            _ => Guid.Empty,
        };

        if (organizationId == Guid.Empty)
        {
            logger.LogError("Invalid organization id {OrganizationId}", options.OrganizationId);
            return false;
        }

        var subscriptions = await store.FindForDeliveryAsync(evt.AccountId, organizationId, evt.ObjectType, eventKey);
        if (subscriptions.Count == 0)
        {
            logger.LogInformation("Found no subscriptions in the {Account} for {ObjectType} {EventKey}", evt.AccountId, evt.ObjectType, eventKey);
            return true;
        }

        var objectType = context.ObjectType;

        foreach (var subscription in subscriptions)
        {
            var profileContext = ProfileContext.Create(
                subscription.ProfileId,
                evt.AccountId,
                subscription.EntityId, /* user id */
                subscription.ClientId,
                subscription.OrganizationId
            );

            var flat = await objectTypeService.GetFlatObjectAsync(profileContext, objectType, evt.TargetId);
            if (flat is null)
            {
                // "profile" doesn't have access to this object, skip
                logger.LogInformation(
                    "{ProfileId} {UserId} {OrganizationId} does not have access to {ObjectType} {ObjectId}",
                    subscription.ProfileId,
                    subscription.EntityId,
                    subscription.OrganizationId,
                    evt.ObjectType,
                    evt.TargetId
                );
                continue;
            }

            await publisher.PublishAsync(
                new WebhookEventData(evt.AccountId, evt.ObjectType, eventKey, flat),
                [subscription]
            );

            logger.LogInformation("Published {ObjectType}/{Event} to subscription for profile {ProfileId}",
                evt.ObjectType, eventKey, subscription.ProfileId);

        }
        
        return true;
    }
}