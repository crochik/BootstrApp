using System;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class LoadWorkOrderOnChangeProcessor : LoadObjectOnChangeProcessor<SfWorkOrderObject>, IOnWorkOrderChangeProcessor
{
    public override string ObjectType => "sf_WorkOrder";

    public LoadWorkOrderOnChangeProcessor(
        ILogger<LoadWorkOrderOnChangeProcessor> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        SalesforceService salesforceService
    ) : base(logger, connection, objectTypeService, salesforceService)
    {
    }

    protected override async Task<IFlowObject> ImportObjectAsync(ImportObject options)
    {
        if (options.Source is not SfWorkOrderObject workOrder)
        {
            // TODO: re-read 
            // ...
            throw new Exception($"Unexpected object: {options.Source?.GetType().FullName}");
        }

        using var scope = _logger.AddScope(new
        {
            WorkOrderId = options.Source.Id,
            SfWorkOrderId = options.Source.ExternalId,
            CurrLeadId = workOrder.LeadId,
            CurrEntityId = workOrder.EntityId,
            CurrAssignedEntityId = workOrder.AssignedEntityId,
        });

        _logger.LogInformation("Import WorkOrder");
        
        var lead = default(Lead);

        if (options.AdditionalContext?.LeadId.HasValue ?? false)
        {
            lead = await _connection.Filter<Lead>()
                .Eq(x => x.AccountId, options.Context.AccountId.Value)
                .Eq(x => x.Id, options.AdditionalContext.LeadId.Value)
                .FirstOrDefaultAsync();
        }

        if (lead == null && workOrder.TryGetProperty<string>(SfWorkOrderObject.AccountIdField, out var accountId))
        {
            lead = await _connection.Filter<Lead>()
                .Eq(x => x.AccountId, options.Context.AccountId.Value)
                .ElemMatchBuilder(x => x.Integrations, q => q
                    .Eq(x => x.ExternalId, accountId)
                    .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
                )
                .FirstOrDefaultAsync();
        }

        // FCI ONLY, fallback 
        if (lead == null && workOrder.TryGetProperty<string>(SfWorkOrderObject.LeadIdField, out var leadId))
        {
            lead = await _connection.Filter<Lead>()
                .Eq(x => x.AccountId, options.Context.AccountId.Value)
                .ElemMatchBuilder(x => x.Integrations, q => q
                    .Eq(x => x.ExternalId, leadId)
                    .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
                )
                .FirstOrDefaultAsync();
        }

        if (lead == null)
        {
            // TODO: eventually we could load the lead on demand
            // ...
            _logger.LogInformation("Lead not found, skip for now");
            return null;
        }

        Guid? organizationId = options.AdditionalContext?.OrganizationId ?? lead.EntityId;
        if (organizationId.Value == options.Context.AccountId.Value)
        {
            organizationId = null;
        }
        
        if (!organizationId.HasValue && workOrder.TryGetProperty<string>(SfWorkOrderObject.ServiceTerritoryIdField, out var territoryId))
        {
            var organization = await _connection.Filter<Entity, Organization>()
                .Eq(x => x.AccountId, options.Context.AccountId.Value)
                .ElemMatchBuilder(x => x.Identities, q => q
                    .Eq(x => x.ExternalId, territoryId)
                    .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Salesforce))
                )
                .FirstOrDefaultAsync();

            organizationId = organization?.Id;
        }

        var userId = options.AdditionalContext?.UserId;
        if (!userId.HasValue && lead.AssignedEntityId.HasValue)
        {
            var user = await _connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, options.Context.AccountId.Value)
                .Eq(x=>x.Id, lead.AssignedEntityId.Value)
                .FirstOrDefaultAsync();

            userId = user?.Id;
            organizationId ??= user?.OrganizationId;
        }
        
        // FCI only
        if (!userId.HasValue && workOrder.TryGetProperty<string>(SfWorkOrderObject.UserIdField, out var sfUserId))
        {
            var user = await _connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, options.Context.AccountId.Value)
                .ElemMatchBuilder(x => x.Identities, q => q
                    .Eq(x => x.ExternalId, sfUserId)
                    .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Salesforce))
                )
                .FirstOrDefaultAsync();

            userId = user?.Id;
            organizationId ??= user?.OrganizationId;
        }
        
        var query = _connection.Filter<SfWorkOrderObject>(options.ObjectType.CollectionName, options.ObjectType.DatabaseName)
                .Eq(x => x.Id, workOrder.Id)
                .Eq(x => x.AccountId, workOrder.AccountId)
                .Eq(x => x.LastModifiedOn, workOrder.LastModifiedOn)
                .Update
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            ;

        var changed = false;
        if (!workOrder.LeadId.HasValue || lead.Id != workOrder.LeadId.Value)
        {
            _logger.LogInformation("{LeadId} changed", lead.Id);
            query.Set(x => x.LeadId, lead.Id);
            changed = true;
        }

        if (userId.HasValue && (!workOrder.AssignedEntityId.HasValue || workOrder.AssignedEntityId.Value != userId.Value))
        {
            _logger.LogInformation("{AssignedEntityId} changed", userId.Value);
            query.Set(x => x.AssignedEntityId, userId.Value);
            changed = true;
        }

        if (organizationId != workOrder.EntityId)
        {
            _logger.LogInformation("{EntityId} changed", organizationId);
            query.Set(x => x.EntityId, organizationId);
            changed = true;
        }

        if (!changed)
        {
            _logger.LogInformation("Nothing changed");
            return workOrder;
        }

        workOrder = await query.UpdateAndGetOneAsync();
        if (workOrder == null)
        {
            _logger.LogInformation("Failed to update, probably was changed since");
        }
        else
        {
            _logger.LogInformation("WorkOrder changed");
        }

        return workOrder;
    }
}

public class SfWorkOrderObject : SalesforceCustomObject, ILeadReference, IAssignedEntityId
{
    // Properties|... 
    public const string ServiceTerritoryIdField = "ServiceTerritoryId";
    public const string AccountIdField = "AccountId";

    public const string LeadIdField = "INET_Lead__c";
    public const string ServiceResourceIdField = "Design_Associate__c";
    public const string UserIdField = "Design_Associate_User__c";

    public Guid? LeadId { get; set; }
    public Guid? AssignedEntityId { get; set; }
}