using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Data.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class LoadLeadOnChangeProcessor : LoadObjectOnChangeProcessor<SalesforceCustomObject>, IOnLeadChangeProcessor
{
    private readonly LeadBuilderService _leadBuilderService;
    private readonly LeadObjectImporter _objectImporter;
    public override string ObjectType => "sf_Lead";

    public LoadLeadOnChangeProcessor(
        ILogger<LoadLeadOnChangeProcessor> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        SalesforceService salesforceService,
        LeadBuilderService leadBuilderService
    ) : base(logger, connection, objectTypeService, salesforceService)
    {
        _leadBuilderService = leadBuilderService;
    }

    protected override async Task<IFlowObject> ImportObjectAsync(ImportObject options)
    {
        var lead = await GetLeadAsync(options.Context, options.Source.ExternalId);
        if (lead != null)
        {
            _logger.LogInformation("Found {LeadId} for {LeadExternalId}: Update it", lead.Id, options.Source.ExternalId);
            return await UpdateLeadAsync(options.Context, options.Source, lead);
        }

        _logger.LogInformation("Lead with {LeadExternalId} not found", options.Source.ExternalId);
        return await ImportLeadAsync(options.Context, options.Source, null);
    }

    public async Task<Lead> ImportLeadAsync(IEntityContext context, SalesforceCustomObject sfLead, SalesforceCustomObject sfAccount)
    {
        if (sfLead == null)
        {
            if (sfAccount != null)
            {
                // TODO: create lead with sfAccount and add both ids
                // ...
            }

            _logger.LogError("Couldn't find Lead, can't import account");
            return null;
        }

        var json = JsonConvert.SerializeObject(sfLead.Properties, Formatting.None, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        // TODO: the lead type id should be by account
        // ...
        var leadType = await _connection.Filter<LeadType>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, LeadTypeIds.Salesforce)
            .FirstOrDefaultAsync();

        if (leadType == null)
        {
            _logger.LogError("Couldn't find LeadType for Salesforce Leads");
            return null;
        }

        var builder = await _leadBuilderService.AddAsync(context, leadType, json, fireEvents: false);

        if (builder.Failed)
        {
            _logger.LogError("Failed: {Error}", builder.Error);
            return null;
        }

        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, builder.LeadId)
            .FirstOrDefaultAsync();

        if (sfAccount == null) return lead;

        _logger.LogInformation("Add {SfAccountId} integration to Lead", sfAccount.ExternalId);

        var updated = await _connection.Filter<Lead>()
            .Eq(x => x.Id, lead.Id)
            .NotBuilder(q => q.ElemMatchBuilder(x => x.Integrations, q => q.Eq(x => x.ExternalId, sfAccount.ExternalId)))
            .Update
            .Push(x => x.Integrations, new LeadIntegration
            {
                IntegrationId = IntegrationIds.Salesforce,
                ExternalId = sfAccount.ExternalId,
                Tag = "Account",
                Data = sfAccount?.Properties, // 
            })
            .UpdateAndGetOneAsync();

        return updated ?? lead;
    }

    private async Task<Lead> GetLeadAsync(IEntityContext context, string externalId)
    {
        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .ElemMatchBuilder(
                x => x.Integrations,
                q => q
                    .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
                    .Eq(x => x.ExternalId, externalId)
            )
            .FirstOrDefaultAsync();
        return lead;
    }

    public async Task<Lead> UpdateLeadAsync(IEntityContext context, SalesforceCustomObject source, Lead lead)
    {
        using var scope = _logger.AddScope(new
        {
            source.ObjectType,
            source.ExternalId,
            source.Id,
            LeadId = lead.Id,
        });

        _logger.LogInformation("Update Lead");

        if (source.ObjectType == "sf_Account")
        {
            // Account
            lead = await MarkLeadAsConvertedAsync(context, lead);
        }
        else if (source.TryGetProperty<string>("ConvertedAccountId", out var convertedAccountId) && !string.IsNullOrWhiteSpace(convertedAccountId))
        {
            // Lead.ConvertedAccountId
            _logger.LogInformation("Lead was converted: {ConvertedAccountId}", convertedAccountId);

            // Converted date is "just a date" so always use the "now" 
            // if (!source.TryGetProperty<DateTime?>("ConvertedDate", out var convertedDate))
            // {
            //     _logger.LogInformation("Couldn't get ConvertedDate from sfLead, using NOW");
            // }
            //
            // lead = await MarkLeadAsConvertedAsync(context, lead, convertedDate);
            lead = await MarkLeadAsConvertedAsync(context, lead);
        }

        var modifiedFields = new Dictionary<string, object>();
        var query = _connection.Filter<Lead>()
            .Eq(x => x.Id, lead.Id)
            // to avoid updating stale lead
            .Lte(x => x.LastModifiedOn, lead.LastModifiedOn)
            .Update;

        var isActive = lead.IsActive;

        // Lead.Status
        if (source.TryGetProperty<string>("Status", out var status))
        {
            // if status exists, use it to set isActive
            isActive = status switch
            {
                "Dead" => false,
                // "New"
                // "Converted" : "Converted",
                // "In Progress" : "In Progress"
                _ => true,
            };
        }

        // *.IsDeleted
        if (source.TryGetProperty<bool>("IsDeleted", out var isDeleted) && isDeleted)
        {
            // mark as inactive it is deleted
            isActive = false;
        }

        if (isActive != lead.IsActive)
        {
            modifiedFields.Add(nameof(Lead.IsActive), isActive);
            query.Set(x => x.IsActive, isActive);
        }

        // Account (integration)
        if (source.ObjectType == "sf_Account")
        {
            var sfAccount = lead.Integrations?.FirstOrDefault(x => x.IntegrationId == IntegrationIds.Salesforce && x.ExternalId == source.ExternalId);
            if (sfAccount == null)
            {
                _logger.LogInformation("Add missing Account integration to Lead");
                modifiedFields.Add(nameof(Lead.Integrations), $"Salesforce:Account:{source.ExternalId}");
                query.AddToSet(x => x.Integrations, new LeadIntegration
                    {
                        IntegrationId = IntegrationIds.Salesforce,
                        ExternalId = source.ExternalId,
                        Tag = "Account",
                        Data = source.Properties, // they will not match singer and we probably do not need a copy of it, but... 
                    }
                );
            }
        }
        
        UpdateCommunicationPreferencesWithInstructions(context, source, lead, modifiedFields, query);

        if (modifiedFields.Count < 1)
        {
            _logger.LogInformation("No meaningful changes, ignore");
            return lead;
        }

        var modifiedLead = await query
            .Set(x => x.LastModifiedOn, DateTime.Now)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        if (modifiedLead == null)
        {
            _logger.LogInformation("Failed to update {LeadId}, it has been updated since {LastModifiedOn}", lead.Id, lead.LastModifiedOn);
            return null;
        }

        await _objectTypeService.FireObjectUpdatedAsync(context, modifiedLead, modifiedFields, evt =>
        {
            evt.Description = "Lead updated with Salesforce changes";

            evt.SetRefValue(nameof(Integration), IntegrationIds.Salesforce);
            evt.SetMetaValue(nameof(Integration), IntegrationIds.GetName(IntegrationIds.Salesforce));
        });

        _logger.LogInformation("{LeadId} updated", modifiedLead.Id);

        return modifiedLead;
    }

    /// <summary>
    /// Update Properties that may have changed
    /// Hardcoded for FCI right now
    /// </summary>
    private static void UpdateCommunicationPreferencesWithInstructions(IEntityContext context, SalesforceCustomObject source, Lead lead, Dictionary<string, object> modifiedFields, UpdateQuery<Lead> query)
    {
        // Lead.DoNotCall
        if (source.TryGetProperty<bool?>("DoNotCall", out var doNotCall) && doNotCall.GetValueOrDefault())
        {
            if (lead.GetCommunicationPreference(CommunicationChannel.Phone) != CommunicationPreference.OptedOut)
            {
                modifiedFields.Add($"{nameof(Lead.CommunicationPreferences)}|{CommunicationChannel.Phone}", CommunicationPreference.OptedOut);
                query.Set(x => x.CommunicationPreferences[CommunicationChannel.Phone], CommunicationPreference.OptedOut);
            }
        }
        
        // Lead.et4ae5__HasOptedOutOfMobile__c
        // ...
        
        // Lead or Account
        if (source.TryGetProperty<string>("E_mail_Instructions__c", out var emailInstructions))
        {
            var emailPreference = emailInstructions switch
            {
                "No Restrictions" => CommunicationPreference.OptedIn,
                "DO NOT SEND" => CommunicationPreference.OptedOut,
                _ => CommunicationPreference.Unknown,
            };

            if (lead.GetCommunicationPreference(CommunicationChannel.Email) != emailPreference)
            {
                modifiedFields.Add($"{nameof(Lead.CommunicationPreferences)}|{CommunicationChannel.Email}", emailPreference);
                query.Set(x => x.CommunicationPreferences[CommunicationChannel.Email], emailPreference);
            }
        }

        // Lead or Account
        if (source.TryGetProperty<string>("Call_Instructions__c", out var callInstructions))
        {
            var callPreference = callInstructions switch
            {
                "No Restrictions" => CommunicationPreference.OptedIn,
                "DO NOT CALL" => CommunicationPreference.OptedOut,
                _ => CommunicationPreference.Unknown,
            };

            if (lead.GetCommunicationPreference(CommunicationChannel.Phone) != callPreference)
            {
                modifiedFields.Add($"{nameof(Lead.CommunicationPreferences)}|{CommunicationChannel.Phone}", callPreference);
                query.Set(x => x.CommunicationPreferences[CommunicationChannel.Phone], callPreference);
            }
        }
    }

    public async Task<Lead> MarkLeadAsConvertedAsync(IEntityContext context, Lead lead, DateTime? convertedDate = null)
    {
        if (lead.ConvertedOn.HasValue)
        {
            _logger.LogInformation("{LeadId} has already been flagged as {ConvertedDate}", lead.Id, lead.ConvertedOn);
            return lead;
        }

        convertedDate ??= DateTime.UtcNow;

        var updatedLead = await _connection.Filter<Lead>()
            .Eq(x => x.Id, lead.Id)
            .OrBuilder(
                q => q.Eq(x => x.ConvertedOn, null),
                q => q.Gt(x => x.ConvertedOn, convertedDate)
            )
            .Update
            .Set(x => x.ConvertedOn, convertedDate)
            .Set(x => x.LastModifiedOn, DateTime.Now)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        if (updatedLead != null)
        {
            var modifiedFields = new Dictionary<string, object>
            {
                { nameof(Lead.ConvertedOn), convertedDate },
            };

            _logger.LogInformation("{LeadId} was marked as {ConvertedOn}", lead.Id, convertedDate);
            await _objectTypeService.FireObjectUpdatedAsync(context, updatedLead, modifiedFields, evt =>
            {
                evt.Description = "Lead marked as converted because of changes in Salesforce";
                evt.SetRefValue(nameof(Integration), IntegrationIds.Salesforce);
                evt.SetMetaValue(nameof(Integration), IntegrationIds.GetName(IntegrationIds.Salesforce));
            });

            return updatedLead;
        }

        _logger.LogInformation("{LeadId} was already marked as converted", lead.Id);
        return lead;
    }

    public async Task<Lead> FindLeadBySfAccountIdAsync(IEntityContext context, string sfAccountId = null, SalesforceCustomObject sfAccount = null)
    {
        sfAccountId ??= sfAccount?.ExternalId;
        if (string.IsNullOrWhiteSpace(sfAccountId))
        {
            throw new Exception("Missing required argument");
        }

        var lead = await GetLeadAsync(context, sfAccountId);
        if (lead != null)
        {
            _logger.LogInformation("Found {LeadId} for ServiceAppointment, mark it as converted", lead.Id);
            return lead;
        }

        _logger.LogInformation("Did not find Lead by {SfAccountId}, try to find SfLead...", sfAccountId);

        var sfLead = await GetSfLeadByConvertedAccountAsync(context, sfAccountId);
        if (sfLead == null)
        {
            _logger.LogInformation("Couldn't find SfLead for {SfAccountId}, nothing else we can do", sfAccountId);
            return null;
        }

        _logger.LogInformation("Found {SfLeadId} for {SfAccountId}", sfLead.ExternalId, sfAccountId);

        sfAccount ??= await GetSfAccountAsync(context, sfAccountId);

        lead = await GetLeadAsync(context, sfLead.ExternalId);
        if (lead != null)
        {
            _logger.LogInformation("Add {SfAccountId} integration to Lead", sfAccountId);

            var updated = await _connection.Filter<Lead>()
                .Eq(x => x.Id, lead.Id)
                .NotBuilder(q => q.ElemMatchBuilder(x => x.Integrations, q => q.Eq(x => x.ExternalId, sfAccountId)))
                // .Ne($"{nameof(Lead.Integrations)}.{nameof(LeadIntegration.ExternalId)}", sfAccountId);
                .Update
                .Push(x => x.Integrations, new LeadIntegration
                {
                    IntegrationId = IntegrationIds.Salesforce,
                    ExternalId = sfAccountId,
                    Tag = "Account",
                    Data = sfAccount?.Properties, // 
                })
                .UpdateAndGetOneAsync();

            return updated ?? lead;
        }

        if (sfAccount == null)
        {
            _logger.LogInformation("Didn't find SfAccount, can't do anything else");
            return null;
        }

        _logger.LogInformation("SfLead hasn't been imported yet");
        lead = await ImportLeadAsync(context, sfLead, sfAccount);

        return lead;
    }

    private async Task<SalesforceCustomObject> GetSfLeadByConvertedAccountAsync(IEntityContext context, string sfAccountId)
    {
        var objectType = await _objectTypeService.GetAsync(context, "sf_Lead");

        var sfLead = await _connection.Filter<SalesforceCustomObject>(objectType.CollectionName, objectType.DatabaseName)
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Properties["ConvertedAccountId"], sfAccountId)
            .FirstOrDefaultAsync();

        return sfLead;
    }

    private async Task<SalesforceCustomObject> GetSfAccountAsync(IEntityContext context, string sfAccountId)
    {
        var objectType = await _objectTypeService.GetAsync(context, "sf_Account");

        var sfLead = await _connection.Filter<SalesforceCustomObject>(objectType.CollectionName, objectType.DatabaseName)
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.ExternalId, sfAccountId)
            .FirstOrDefaultAsync();

        return sfLead;
    }
}