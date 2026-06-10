using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using NetCoreForce.Client;
using NetCoreForce.Client.Models;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Models.Interfaces;
using PI.Shared.Salesforce;
using PI.Shared.Salesforce.Models;
using PI.Shared.Services;

namespace Services;

public class SalesforceLeadService : SalesforceService
{
    private const string ApexConvertLeadPath = "/services/apexrest/PI/Lead";
    private const string WorkTypeName = "In Home Consultation";
    private const string WorkTypeId = "08q41000000CaYsAAK";

    private readonly IServiceProvider _serviceProvider;
    private readonly ObjectTypeService _objectTypeService;
    private readonly IOrganizationAdapter _organizationAdapter;
    private readonly ILeadAdapter _leadAdapter;
    private readonly IIntegrationLeadAdapter _integrationLeadAdapter;

    public SalesforceLeadService(
        ILogger<SalesforceLeadService> logger,
        MongoConnection connection,
        IServiceProvider serviceProvider,
        ObjectTypeService objectTypeService,
        NetCoreForceClient salesforceClient,
        IHttpClientFactory httpClientFactory,
        IEntityIdentityAdapter identityAdapter,
        IEntityIntegrationAdapter entityIntegrationAdapter,
        IOrganizationAdapter organizationAdapter,
        ILeadAdapter leadAdapter,
        IIntegrationLeadAdapter integrationLeadAdapter
    ) : base(logger, connection, salesforceClient, httpClientFactory, identityAdapter, entityIntegrationAdapter)
    {
        _serviceProvider = serviceProvider;
        _objectTypeService = objectTypeService;
        _organizationAdapter = organizationAdapter;
        _leadAdapter = leadAdapter;
        _integrationLeadAdapter = integrationLeadAdapter;
    }

    private Dictionary<string, object> MapLead(Lead lead, Organization organization)
    {
        var src = lead.AllProperties()
            .Where(x => x.Value != null)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        // extra lead
        addSrcProp("firstName", lead.GetFirstName());
        addSrcProp("lastName", lead.GetLastName());
        addSrcProp("name", lead.Name);
        addSrcProp("company", lead.Name);
        addSrcProp("id", lead.Id);

        // addSrcProp("schedulerUrl", msg.Lead.);

        // context
        foreach (var kv in GetKeyValuePairs(organization))
        {
            addSrcProp(kv.Key, kv.Value);
        }

        return src;

        void addSrcProp(string srcPath, object srcValue)
        {
            if (srcValue == null) return;
            src.TryAdd(srcPath, srcValue);
        }
    }

    private IEnumerable<KeyValuePair<string, object>> GetKeyValuePairs(Organization organization, string prefix = "context")
    {
        yield return new KeyValuePair<string, object>($"{prefix}.entityId", organization.Id);

        foreach (var identity in organization.GetIdentities())
        {
            var key = $"{prefix}.identity.organization.{identity.IdentityProviderId}";

            yield return new KeyValuePair<string, object>($"{key}.entityId", organization.Id); // why if it is always the same?
            yield return new KeyValuePair<string, object>($"{key}.name", identity.Name);
            yield return new KeyValuePair<string, object>($"{key}.externalId", identity.ExternalId);
        }
    }

    private async Task<Dictionary<string, object>> GetMapAsync(IEntityContext accountContext, ExportToSalesforceAction.Message msg)
    {
        var lead = await _leadAdapter.GetByIdAsync(accountContext, msg.Event.TargetId);
        if (lead == null)
        {
            throw new NotFoundException(nameof(SystemObjectType.Lead), msg.Event.TargetId);
        }

        var organization = await _organizationAdapter.GetByIdAsync(accountContext, lead.EntityId);
        if (organization == null)
        {
            throw new NotFoundException(nameof(SystemObjectType.Organization), lead.EntityId);
        }

        return MapLead(lead, organization);
    }

    public async Task<(string id, string error)> ExportLeadAsync(IEntityContext context, ExportToSalesforceAction.Message msg)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId = msg.Event.TargetId,
        });

        _logger.LogInformation("Export Lead to Salesforce");

        var leadMap = await GetMapAsync(context, msg);

        return await ExportLeadAsync(context, msg, msg.Event.TargetId, leadMap);
    }

    private async Task<(string id, string error)> ExportLeadAsync(IEntityContext context, ExportToSalesforceAction.Message msg, Guid leadId, Dictionary<string, object> leadMap)
    {
        try
        {
            var result = await ExportLeadToSalesforceAsync(msg.Event.AccountId, leadMap, msg.Options);
            if (result.error != null)
            {
                _logger.LogError("Failed to export lead to salesforce: {Error}", result.error);
                return await saveErrorToIntegrationAsync(result.error);
            }

            var added = await _integrationLeadAdapter.AddAsync(context, new LeadIntegration
            {
                ExternalId = result.id,
                LeadId = leadId,
                IntegrationId = IntegrationIds.Salesforce,
                CreatedOn = DateTime.UtcNow,
                Status = "Exported",
                Tag = nameof(Lead),

                // TODO: move to integration or account identity?
                // ...
                // Url = $"https://fcifloors.my.salesforce.com/{result.id}",
            });

            if (added == null)
            {
                _logger.LogError("Lead was exported but failed to update status in database");
                return await saveErrorToIntegrationAsync("Failed to add integration info to lead");
            }

            _logger.LogInformation("Lead exported to salesforce");
            return result;
        }
        catch (ForceApiException ex)
        {
            _logger.LogCritical(ex, "Failed to export lead");
            return await saveErrorToIntegrationAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export lead");
            return await saveErrorToIntegrationAsync(ex.Message);
        }

        async Task<(string id, string error)> saveErrorToIntegrationAsync(string error)
        {
            // TODO: change to upsert so we can update the last error
            // ...s
            await _integrationLeadAdapter.AddAsync(context, new LeadIntegration
            {
                ExternalId = leadId.ToString(),
                LeadId = leadId,
                IntegrationId = IntegrationIds.Salesforce,
                CreatedOn = DateTime.UtcNow,
                Tag = "Error",
                Status = error
            });

            return (null, error);
        }
    }

    private async Task<(string id, string error)> ExportLeadToSalesforceAsync(Guid accountId, Dictionary<string, object> src, ExportToSalesforceActionOptions options)
    {
        var (token, error) = await GetTokenAsync(accountId);
        if (token == null)
        {
            return (null, error);
        }

        var meta = default(SObjectDescribeFull);
        if (options.MapAllFields)
        {
            meta = await SalesforceClient.DescribeAsync(token, nameof(Lead));
            if (meta == null)
            {
                _logger.LogError("Failed to get metadata for Lead");
                return (null, "failed to get metadata");
            }
        }

        var expando = BuildSalesforceLeadObject(src, options, meta);
        return await SalesforceClient.CreateAsync(token, nameof(Lead), expando);
    }

    /// <summary>
    /// Export Lead to salesforce
    /// body is already processed and will be sent "as it is"
    /// </summary>
    public async Task<Result<string>> ExportLeadToSalesforceAsync(IEntityContext context, IDictionary<string, object> body)
    {
        var (token, error) = await GetTokenAsync(context.AccountId.Value);
        if (token == null)
        {
            return Result.Error<string>(error);
        }

        var (salesforceId, createError) = await SalesforceClient.CreateAsync(token, nameof(Lead), body);
        return string.IsNullOrEmpty(salesforceId) ? Result.Error<string>(createError) : Result.Success(salesforceId);
    }

    public async Task<Result<ConvertLeadResponse>> ConvertLeadAsync(IEntityContext context, ConvertLeadRequest request)
    {
        var (token, error) = await GetTokenAsync(context.AccountId.Value);
        if (token == null)
        {
            return Result.Error<ConvertLeadResponse>(error);
        }

        var response = await SalesforceClient.PostAsync<ConvertLeadResponse>(token, ApexConvertLeadPath, request);
        if (!string.IsNullOrWhiteSpace(response.Error))
        {
            _logger.LogError("Failed to convert lead: {Error}", response.Error);
            throw new Exception($"Failed to convert lead: {response.Error}");
        }

        return Result.Success(response);
    }

    private object BuildSalesforceLeadObject(Dictionary<string, object> src, ExportToSalesforceActionOptions options, SObjectDescribeFull meta)
    {
        dynamic expando = new ExpandoObject();
        var idict = (IDictionary<string, object>)expando;

        var sfFields = meta?.Fields
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var mapping = options.PropertiesMapping?
            .ToDictionary(x => x.Key.Replace('|', '.'), x => x.Value);

        if (mapping != null)
        {
            // explicit fields
            foreach (var dstField in mapping)
            {
                string type = null;
                if (sfFields != null && sfFields.TryGetValue(dstField.Key, out var field))
                {
                    if (field.Calculated || !field.Creatable || !field.Updateable)
                    {
                        _logger.LogInformation("Field can't be set {Field}", dstField.Key);
                        continue;
                    }

                    type = field.Type;
                }

                var value = resolveValue(dstField.Value);
                idict.AddSubPath(dstField.Key, Convert(options, type, value));
            }
        }

        if (sfFields != null)
        {
            foreach (var field in sfFields.Values)
            {
                if (field.Calculated || !field.Creatable || !field.Updateable)
                {
                    continue;
                }

                if (mapping != null && mapping.ContainsKey(field.Name))
                {
                    // has already been mapped
                    continue;
                }

                if (src.TryGetValue(field.Name, out var srcProp))
                {
                    if (srcProp == null) continue;
                    idict.TryAdd(field.Name, Convert(options, field.Type, srcProp));
                }
            }
        }

        var json = JsonConvert.SerializeObject(idict);
        _logger.LogInformation("Create Lead: {Json}", json);
        return expando;

        object resolveValue(string srcValue)
        {
            if (srcValue.StartsWith("="))
            {
                // "formula"
                if (srcValue.StartsWith("='") && srcValue.EndsWith("'"))
                {
                    return srcValue.Substring(2, srcValue.Length - 3);
                }
            }

            if (src.TryGetValue(srcValue, out var srcProp))
            {
                return srcProp;
            }

            _logger.LogInformation("Missing src prop {Field}", srcValue);
            return null;
        }
    }

    private object Convert(ExportToSalesforceActionOptions options, string type, object value)
    {
        switch (type)
        {
            case "phone":
                if (options.ForcePlainPhoneNumber) return GetPlainPhoneNumber(value);
                break;
        }

        return value;
    }

    private object GetPlainPhoneNumber(object value)
    {
        if (value is string str)
        {
            return GetPlainPhoneNumber(str);
        }

        return value;
    }

    public static object GetPlainPhoneNumber(string str)
    {
        if (str == null) return null;

        var build = new StringBuilder();
        foreach (var c in str)
        {
            if (!char.IsDigit(c)) continue;
            build.Append(c);
        }

        return build.ToString();
    }

    public async Task<(string id, string error)> ExportAppointmentAsync(IEntityContext context, ExportToSalesforceAction.Message msg)
    {
        using var scope = _logger.AddScope(new
        {
            AppointmentId = msg.Event.TargetId,
        });

        _logger.LogInformation("Export appointment to Salesforce");

        try
        {
            var appointment = await ExportAppointmentAsync(context, msg, msg.Event.TargetId);
            var id = appointment?
                .Integrations?
                .FirstOrDefault(x => x.IntegrationId == IntegrationIds.Salesforce)?
                .ExternalId;

            return (id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export appointment to sf");
            return (null, ex.Message);
        }
    }

    /// <summary>
    /// Export appointment to Salesforce
    /// NOTE: will return null if no update is performed
    /// </summary>
    private async Task<Appointment> ExportAppointmentAsync(IEntityContext context, ExportToSalesforceAction.Message msg, Guid id)
    {
        var appointment = await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (appointment == null)
        {
            _logger.LogError("Couldn't load appointment");
            throw new NotFoundException(nameof(Appointment), id);
        }

        using var scope = _logger.AddScope(new
        {
            appointment.LeadId,
            appointment.IsActive,
            appointment.CancelledOn,
        });

        var sfIntegration = appointment.Integrations?.FirstOrDefault(x => x.IntegrationId == IntegrationIds.Salesforce);
        if (!appointment.IsActive || appointment.CancelledOn.HasValue)
        {
            _logger.LogInformation("Appointment is inactive: {CancelledOn}", appointment.CancelledOn);
            if (sfIntegration == null)
            {
                _logger.LogInformation("Skip exporting as it has never been");
                return null;
            }

            if (sfIntegration.Status == "Cancelled")
            {
                _logger.LogInformation("Integration has alreasdy been updated");
                return null;
            }
        }
        else if (sfIntegration != null)
        {
            _logger.LogInformation("Appointment has already been exported, do not update (unless it has been cancelled");
            return null;
        }

        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, appointment.LeadId)
            .FirstOrDefaultAsync();

        if (lead == null)
        {
            _logger.LogError("Failed to load {LeadId} for Appointment", appointment.LeadId);
            throw new NotFoundException(nameof(Lead), appointment.LeadId);
        }

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, appointment.EntityId)
            .FirstOrDefaultAsync();

        if (user == null || !user.OrganizationId.HasValue)
        {
            _logger.LogError("Failed to load {UserId} (or doesn't belong to Org)", appointment.EntityId);
            throw new NotFoundException(nameof(User), appointment.EntityId);
        }

        var (token, error) = await GetTokenAsync(AccountIds.FCI);
        if (error != null)
        {
            _logger.LogCritical("Failed to get Token: {Error}", error);
            throw new Exception($"Failed to get Token: {error}");
        }

        var builder = new AppointmentBuilder
        {
            Context = context,
            Action = msg,
            User = user,
            Token = token,
            Appointment = appointment,
            Lead = lead,
        };

        var result = await ExportAppointmentAsync(builder);

        try
        {
            var additionalContext = new AdditionalContext
            {
                LeadId = builder.Lead?.Id,
                AppointmentId = builder.Appointment?.Id,
                SfServiceAppointmentId = builder.SfServiceAppointmentId,
                SfWorkOrderId = builder.SfWorkOrderId,
                SfAccountId = builder.SfAccountId,
                SfLeadId = builder.SfLeadId,
                UserId = user.Id,
                OrganizationId = user.OrganizationId,
            };

            await LoadAccountAsync(builder.Context.AccountId.Value, additionalContext);
            await LoadWorkOrderAsync(builder.Context.AccountId.Value, additionalContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load work order");
        }

        return result;
    }

    private async Task GetOrCreateSfLeadAsync(LeadBuilder builder)
    {
        if (builder.SfLeadId != null) return;

        _logger.LogInformation("{LeadId} hasn't been exported to Salesforce, export it.", builder.Lead.Id);
        builder.Organization ??= await _organizationAdapter.GetByIdAsync(builder.Context, builder.Lead.EntityId);

        var leadMap = MapLead(builder.Lead, builder.Organization);
        (builder.SfLeadId, var error) = await ExportLeadAsync(builder.Context, builder.Action, builder.Lead.Id, leadMap);
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("Failed to export Lead to Salesforce: {Error}", error);
            throw new BadRequestException("Failed to Export Lead");
        }

        _logger.LogInformation("Lead exported to Salesforce: {sfLeadId}", builder.SfLeadId);
    }

    private async Task GetLatestSfWorkOrderAsync(AppointmentBuilder builder)
    {
        // get WorkOrder
        var sosql = $"SELECT Id FROM WorkOrder WHERE Accountid='{builder.SfAccountId}' ORDER BY CreatedDate DESC LIMIT 1";
        var workOrders = await SalesforceClient.QueryAllAsync<SfWorkOrder>(builder.Token, sosql);
        var workOrder = workOrders?.FirstOrDefault();
        if (string.IsNullOrEmpty(workOrder?.Id))
        {
            _logger.LogError("Couldn't find WorkOrder: {sosql}", sosql);
            throw new Exception("Couldn't find last Salesforce WorkOrder");
        }

        _logger.LogInformation("Using {SfWorkOrderId}", workOrder.Id);
        builder.SfWorkOrderId = workOrder.Id;
    }

    private async Task CreateSfWorkOrderLineItemAsync(AppointmentBuilder builder)
    {
        // var sosql = $"SELECT Id FROM WorkOrderLineItem WHERE WorkOrderId='{builder.SfWorkOrderId}' ORDER BY CreatedDate DESC LIMIT 1";
        // var workOrderLineItems = await _salesforceClient.QueryAllAsync<SfWorkOrderLineItem>(builder.Token, sosql);
        // if (workOrderLineItems.Count > 0)
        // {
        //     var workOrderLineItem = workOrderLineItems?.FirstOrDefault();
        //     if (string.IsNullOrEmpty(workOrderLineItem?.Id))
        //     {
        //         _logger.LogError("Couldn't find WorkOrderLineItem: {sosql}", sosql);
        //         throw new Exception("Couldn't find last Salesforce WorkOrderLineItem");
        //     }
        //
        //     _logger.LogInformation("Using {SfWorkOrderLineItemId}", workOrderLineItem.Id);
        //     builder.SfWorkOrderLineItemId = workOrderLineItem.Id;
        //     return;
        // }

        _logger.LogInformation("Create Work Order Line Item");

        // TODO: fci only...
        var expando = new ExpandoObject();
        var iDict = (IDictionary<string, object>)expando;
        iDict["workTypeId"] = WorkTypeId;
        iDict["workOrderId"] = builder.SfWorkOrderId;
        iDict["serviceTerritoryId"] = builder.SfTerritoryId;
        iDict["design_Associate__c"] = builder.SfServiceMemberId;

        var (id, error) = await SalesforceClient.CreateAsync(builder.Token, "WorkOrderLineItem", expando);
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("Failed to create WorkOrderLineItem: {Error}", error);
            throw new Exception("Failed to create Work Order Line Item");
        }

        _logger.LogInformation("Created {SfWorkOrderLineItemId}", id);
        builder.SfWorkOrderLineItemId = id;
    }

    private async Task<bool> GetSfAccountAsync(AppointmentBuilder builder)
    {
        if (builder.SfAccountId != null) return true;

        var sosql = $"SELECT ConvertedAccountId FROM Lead WHERE Id='{builder.SfLeadId}'";
        var sfLeads = await SalesforceClient.QueryAllAsync<SfLead>(builder.Token, sosql);
        if (sfLeads.Count != 1) throw new Exception("Lead not found");

        var sfLead = sfLeads?.FirstOrDefault();
        if (!string.IsNullOrEmpty(sfLead?.ConvertedAccountId))
        {
            builder.SfAccountId = sfLead.ConvertedAccountId;
            return true;
        }

        return false;
    }

    private async Task CreateNewWorkOrderAsync(AppointmentBuilder builder)
    {
        var sfUserId = builder.SfUserId;
        if (string.IsNullOrWhiteSpace(sfUserId))
        {
            _logger.LogError("Salesforce Identity not found for user");
            throw new BadRequestException("Salesforce Identity not found for user");
        }

        var sfServiceMemberId = builder.SfServiceMemberId;
        if (string.IsNullOrWhiteSpace(sfServiceMemberId))
        {
            _logger.LogError("Salesforce ServiceResource not found for user");
            throw new BadRequestException("Salesforce ServiceResource not found for user");
        }

        await GetSfTerritoryAsync(builder);

        // TODO: fci only...
        var expando = new ExpandoObject();
        var iDict = (IDictionary<string, object>)expando;
        iDict["INET_Lead__c"] = builder.SfLeadId;
        iDict["AccountId"] = builder.SfAccountId;
        iDict["ServiceTerritoryId"] = builder.SfTerritoryId;
        iDict["Design_Associate__c"] = sfServiceMemberId;
        iDict["OwnerId"] = sfUserId;
        var (id, error) = await SalesforceClient.CreateAsync(builder.Token, "WorkOrder", expando);
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("Failed to create WorkOrder: {Error}", error);
            throw new Exception("Failed to create Work Order");
        }

        _logger.LogInformation("Created {SfWorkOrderId}", id);
        builder.SfWorkOrderId = id;
    }

    private async Task ConvertLeadToAccountAsync(AppointmentBuilder builder)
    {
        _logger.LogInformation("Lead hasn't been converted yet, convert");

        var sfUserId = builder.SfUserId;
        if (string.IsNullOrWhiteSpace(sfUserId))
        {
            _logger.LogError("Salesforce Identity not found for user");
            throw new BadRequestException("Salesforce Identity not found for user");
        }

        var sfServiceMemberId = builder.SfServiceMemberId;
        if (string.IsNullOrWhiteSpace(sfServiceMemberId))
        {
            _logger.LogError("Salesforce ServiceResource not found for user");
            throw new BadRequestException("Salesforce ServiceResource not found for user");
        }

        await GetSfTerritoryAsync(builder);

        var body = new ConvertLeadRequest
        {
            LeadId = builder.SfLeadId,
            OwnerId = sfUserId,
            ServiceTerritoryId = builder.SfTerritoryId,
            ServiceMemberId = sfServiceMemberId,
            WorkTypeName = WorkTypeName,
        };

        // TODO: hack for fci...
        // TODO: should probably use the objecttypeservice and configuration but....
        body.LeadSource = builder.Lead[Lead.PropertyName_LeadSource] ?? builder.Lead[Lead.PropertyName_HowDidYouHearAboutUs];

        // if (builder.Lead.Properties.TryGetValue(Lead.PropertyName_LeadSource, out var leadSourceId))
        // {
        //     // TODO: should probably use the objecttypeservice and configuration but....
        //     // var leadSourceRow = await _connection.Filter<CustomObject>()
        //     //     .Eq(x => x.AccountId, context.AccountId.Value)
        //     //     .Eq(x => x.ObjectType, "SfLeadSource")
        //     //     .Eq(x => x.ExternalId, leadSourceId.ToString())
        //     //     .FirstOrDefaultAsync();
        //     // body.LeadSource = leadSourceRow?.Name;
        //     body.LeadSource = leadSourceId.ToString();
        // } 
        // else if (builder.Lead.Properties.TryGetValue(Lead.PropertyName_HowDidYouHearAboutUs, out leadSourceId))
        // {
        //     // fallaback to hdyhau (as it is the only one we really need... )
        //     body.LeadSource = leadSourceId.ToString();
        // }

        _logger.LogInformation("Convert lead: {Request}", JsonConvert.SerializeObject(body));

        var response = await SalesforceClient.PostAsync<ConvertLeadResponse>(builder.Token, ApexConvertLeadPath, body);
        if (!string.IsNullOrWhiteSpace(response.Error))
        {
            _logger.LogError("Failed to convert lead: {Error}", response.Error);
            throw new Exception($"Failed to convert lead: {response.Error}");
        }

        _logger.LogInformation(
            "Lead Converted: {SfAccountId} {SfContactId} {SfOpportunityId} {SfWorkOrderId} {SfWorkOrderLineId}",
            response.AccountId,
            response.ContactId,
            response.OpportunityId,
            response.WorkOrderId,
            response.WorkOrderLineId
        );

        var integrations = new List<LeadIntegration>();

        if (!string.IsNullOrWhiteSpace(response.AccountId))
        {
            integrations.Add(new LeadIntegration
            {
                IntegrationId = IntegrationIds.Salesforce,
                ExternalId = response.AccountId,
                Tag = "Account",
                CreatedOn = DateTime.UtcNow,
                Url = $"{builder.Token.InstanceUrl}/{response.AccountId}",
            });

            builder.SfAccountId = response.AccountId;
        }

        if (!string.IsNullOrWhiteSpace(response.ContactId))
        {
            integrations.Add(new LeadIntegration
            {
                IntegrationId = IntegrationIds.Salesforce,
                ExternalId = response.ContactId,
                Tag = "Contact",
                CreatedOn = DateTime.UtcNow,
                Url = $"{builder.Token.InstanceUrl}/{response.ContactId}",
            });
        }

        if (!string.IsNullOrWhiteSpace(response.OpportunityId))
        {
            integrations.Add(new LeadIntegration
            {
                IntegrationId = IntegrationIds.Salesforce,
                ExternalId = response.OpportunityId,
                Tag = "Opportunity",
                CreatedOn = DateTime.UtcNow,
                Url = $"{builder.Token.InstanceUrl}/{response.OpportunityId}",
            });
        }

        if (!string.IsNullOrWhiteSpace(response.WorkOrderId))
        {
            integrations.Add(new LeadIntegration
            {
                IntegrationId = IntegrationIds.Salesforce,
                ExternalId = response.WorkOrderId,
                Tag = "WorkOrder",
                CreatedOn = DateTime.UtcNow,
                Url = $"{builder.Token.InstanceUrl}/{response.WorkOrderId}",
            });

            builder.SfWorkOrderId = response.WorkOrderId;
        }

        if (!string.IsNullOrWhiteSpace(response.WorkOrderLineId))
        {
            integrations.Add(new LeadIntegration
            {
                IntegrationId = IntegrationIds.Salesforce,
                ExternalId = response.WorkOrderLineId,
                Tag = "WorkOrderLineItem",
                CreatedOn = DateTime.UtcNow,
                Url = $"{builder.Token.InstanceUrl}/{response.WorkOrderLineId}",
            });

            builder.SfWorkOrderLineItemId = response.WorkOrderLineId;
        }

        builder.Lead = await AddIntegrationsAsync(builder.Context, builder.Lead, integrations, "Lead Converted");

        if (!builder.Lead.Properties.TryGetStrParam("company", out var company) || company != builder.Lead.Name)
        {
            // TODO: hack to handle lead conversion when there is a company defined (FCI)
            _logger.LogInformation("{LeadId} with {Name}  has {Company} defined. Update Account name", builder.Lead.Id, builder.Lead.Name, company);

            await SalesforceClient.UpdateAsync(builder.Token, "Account", builder.SfAccountId, new
            {
                Name = builder.Lead.Name,
            });
        }
    }

    private async Task<Lead> AddIntegrationsAsync(IEntityContext context, Lead lead, IEnumerable<LeadIntegration> integrations, string eventDescription)
    {
        var now = DateTime.UtcNow;
        lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, lead.Id)
            .Update
            .AddToSetEach(x => x.Integrations, integrations)
            .Set(x => x.ConvertedOn, now)
            .Set(x => x.LastModifiedOn, now)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        var modifiedFields = new Dictionary<string, object>
        {
            { nameof(Lead.ConvertedOn), now },
            { nameof(Lead.Integrations), "[...]" },
        };

        await _objectTypeService.FireObjectUpdatedAsync(context, lead, modifiedFields, evt =>
        {
            evt.Description = eventDescription ?? "Integration Added to lead";
            evt.SetRefValue(nameof(Integration), IntegrationIds.Salesforce);
            evt.SetMetaValue(nameof(Integration), IntegrationIds.GetName(IntegrationIds.Salesforce));
        });

        if (lead == null)
        {
            _logger.LogError("Failed to add integrations to lead");
            throw new Exception("Failed to updates lead with salesforce integration Ids");
        }

        _logger.LogInformation("Updated lead record with salesforce ids");

        return lead;
    }

    private async Task GetSfTerritoryAsync(AppointmentBuilder builder)
    {
        builder.Organization ??= await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, builder.Context.AccountId.Value)
            .Eq(x => x.Id, builder.Lead.EntityId)
            .FirstOrDefaultAsync();

        if (builder.Organization == null)
        {
            _logger.LogError("Couldn't find {OrganizationId}", builder.Lead.EntityId);
            throw new NotFoundException(nameof(Organization), builder.Lead.EntityId);
        }

        if (string.IsNullOrWhiteSpace(builder.SfTerritoryId))
        {
            _logger.LogError("Couldn't find sf territory indentiy {OrganizationId}", builder.Organization.Id);
            throw new BadRequestException("Salesforce Identity not found for organization");
        }
    }

    private async Task<Appointment> ExportAppointmentAsync(AppointmentBuilder builder)
    {
        _logger.LogInformation("Export Appointment");

        var createWorkOrder = false;
        var integration = builder.Appointment.Integrations?.FirstOrDefault(x => x.IntegrationId == IntegrationIds.AutoScheduler);
        var data = integration?.Data as BsonDocument;
        if (data != null)
        {
            var dict = data.ToDictionary();
            if (dict.TryGetValue("WorkOrder", out var value) && value is string str && str == "new")
            {
                createWorkOrder = true;
            }
        }

        await GetOrCreateSfLeadAsync(builder);

        var existingAccount = await GetSfAccountAsync(builder);
        if (!existingAccount)
        {
            await ConvertLeadToAccountAsync(builder);
        }
        else if (createWorkOrder)
        {
            _logger.LogInformation("Create New WorkOrder");
            await CreateNewWorkOrderAsync(builder);
        }

        if (string.IsNullOrWhiteSpace(builder.SfWorkOrderId))
        {
            if (builder.Appointment.Refs?.TryGetStrParam("sf_WorkOrder", out var workOrderId) ?? false)
            {
                _logger.LogInformation("Appointment was assigned a {SfWorkOrderId}", workOrderId);
                builder.SfWorkOrderId = workOrderId;
            }
            else
            {
                _logger.LogInformation("Find latest work order");
                await GetLatestSfWorkOrderAsync(builder);
            }
        }

        if (string.IsNullOrWhiteSpace(builder.SfWorkOrderLineItemId))
        {
            await CreateSfWorkOrderLineItemAsync(builder);
        }

        await GetSfTerritoryAsync(builder);

        var sfIntegration = builder.Appointment.Integrations?.FirstOrDefault(x => x.IntegrationId == IntegrationIds.Salesforce);
        if (sfIntegration == null)
        {
            _logger.LogInformation("Create ServiceAppointment");
            await CreateSfAppointmentAsync(builder);
            return builder.Appointment;
        }

        // update appointment in Salesforce
        // right now the only change that we pass on is the appointment status
        _logger.LogInformation("Update existing ServiceAppointment");

        var expando = new ExpandoObject();
        var iDict = (IDictionary<string, object>)expando;

        if (!builder.Appointment.IsActive || builder.Appointment.CancelledOn.HasValue)
        {
            iDict["status"] = "Canceled";
            // iDict["statusCategory"] = "Canceled";
            // ParentRecordStatusCategory
        }

        // update
        await SalesforceClient.UpdateAsync(builder.Token, "ServiceAppointment", sfIntegration.ExternalId, expando);

        builder.Appointment = await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, builder.Context.AccountId.Value)
            .Eq(x => x.Id, builder.Appointment.Id)
            .ElemMatchBuilder
            (
                x => x.Integrations,
                q => q.Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
            )
            .Update
            .Set($"{nameof(Appointment.Integrations)}.$.{nameof(AppointmentIntegration.Status)}", "Cancelled")
            // .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            // .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        return builder.Appointment;
    }

    private async Task LoadAccountAsync(Guid accountId, AdditionalContext additionalContext)
    {
        using var scope = _logger.AddScope(new
        {
            additionalContext.LeadId,
            additionalContext.AppointmentId,
            additionalContext.SfServiceAppointmentId,
            additionalContext.SfWorkOrderId,
            additionalContext.SfAccountId,
            additionalContext.SfLeadId,
        });

        if (string.IsNullOrEmpty(additionalContext.SfAccountId))
        {
            _logger.LogInformation("Didn't get SfAccountId, can't import account");
            return;
        }

        _logger.LogInformation("Import Account from Salesforce");

        using IServiceScope serviceScope = _serviceProvider.CreateScope();
        var processor = serviceScope.ServiceProvider.GetRequiredService<IOnAccountChangeProcessor>();
        var (sf, obj) = await processor.ProcessChangeAsync(
            new ProcessObjectChange
            {
                AccountId = accountId,
                ExternalId = additionalContext.SfAccountId,
                AdditionalContext = additionalContext,
            }
        );

        if (obj == null)
        {
            _logger.LogError("Failed to import Account");
        }

        _logger.LogInformation("Account imported");
    }

    private async Task LoadWorkOrderAsync(Guid accountId, AdditionalContext additionalContext)
    {
        using var scope = _logger.AddScope(new
        {
            additionalContext.LeadId,
            additionalContext.AppointmentId,
            additionalContext.SfServiceAppointmentId,
            additionalContext.SfWorkOrderId,
            additionalContext.SfAccountId,
            additionalContext.SfLeadId,
        });

        if (string.IsNullOrEmpty(additionalContext.SfWorkOrderId))
        {
            _logger.LogInformation("Didn't get SfWorkOrderId, can't import work order");
            return;
        }

        _logger.LogInformation("Import WorkOrder from Salesforce");

        using IServiceScope serviceScope = _serviceProvider.CreateScope();
        var processor = serviceScope.ServiceProvider.GetRequiredService<IOnWorkOrderChangeProcessor>();
        var (sf, obj) = await processor.ProcessChangeAsync(
            new ProcessObjectChange
            {
                AccountId = accountId,
                ExternalId = additionalContext.SfWorkOrderId,
                AdditionalContext = additionalContext,
            }
        );

        if (obj == null)
        {
            _logger.LogError("Failed to import SfWorkOrder");
        }

        _logger.LogInformation("WorkOrder imported");
    }

    /// <summary>
    /// Create appointment   
    /// </summary>
    private async Task CreateSfAppointmentAsync(AppointmentBuilder builder)
    {
        var expando = new ExpandoObject();
        var iDict = (IDictionary<string, object>)expando;

        // create
        iDict["ownerId"] = builder.SfUserId;
        iDict["schedStartTime"] = builder.Appointment.Start;
        iDict["schedEndTime"] = builder.Appointment.End;
        iDict["serviceTerritoryId"] = builder.SfTerritoryId;
        iDict["parentRecordId"] = builder.SfWorkOrderLineItemId;
        iDict["subject"] = builder.Appointment.Name;
        iDict["description"] = builder.Appointment.Description;
        iDict["serviceNote"] = $"Scheduling Tool: {builder.Appointment.Tool}";

        // TODO: move to mapping
        // ...
        // custom properties (FCI Only)
        iDict["design_Associate__c"] = builder.SfServiceMemberId;
        iDict["pIId__c"] = builder.Appointment.Id;

        if (!builder.Appointment.IsActive || builder.Appointment.CancelledOn.HasValue)
        {
            iDict["status"] = "Canceled";
            // iDict["statusCategory"] = "Canceled";
            // ParentRecordStatusCategory
        }

        // string id;
        var sfAppointment = await SalesforceClient.CreateAsync(builder.Token, "ServiceAppointment", expando);
        if (sfAppointment.error != null)
        {
            _logger.LogError("Failed to create ServiceAppointment: {Error}", sfAppointment.error);
            throw new Exception($"Failed to add ServiceAppointment: {sfAppointment.error}");
        }

        _logger.LogInformation("Appointment exported as {ServiceAppointmentId}", sfAppointment.id);
        builder.SfServiceAppointmentId = sfAppointment.id;

        var updateQuery = _connection.Filter<Appointment>()
                .Eq(x => x.AccountId, builder.Context.AccountId.Value)
                .Eq(x => x.Id, builder.Appointment.Id)
                .Update
                .AddToSet(x => x.Integrations, new AppointmentIntegration
                {
                    IntegrationId = IntegrationIds.Salesforce,
                    ExternalId = sfAppointment.id,
                    Url = $"{builder.Token.InstanceUrl}/{builder.SfServiceAppointmentId}",
                })
                .Set(x => x.Refs["sf_WorkOrder"], builder.SfWorkOrderId)
                .Set(x => x.Refs["sf_WorkOrderLineItem"], builder.SfWorkOrderLineItemId)
                .Set(x => x.Refs["sf_User"], builder.SfUserId) // ???? ownerid, not the design associate
                .Set(x => x.Refs["sf_ServiceResource"], builder.SfServiceMemberId)
                .Set(x => x.Refs["sf_ServiceTerritory"], builder.SfTerritoryId)
                .Set(x => x.Refs["sf_ServiceAppointment"], builder.SfServiceAppointmentId)
            // design associate? 
            // ... 
            // accountid
            // ...
            // leadid 
            // ...
            ;

        if (!string.IsNullOrEmpty(builder.SfWorkOrderId))
        {
            // update parent object
            updateQuery.Set(x => x.Parent, new ReferencedObject
            {
                ObjectType = "salesforce.WorkOrder",
                ObjectId = builder.SfWorkOrderId,
            });
        }

        builder.Appointment = await updateQuery.UpdateAndGetOneAsync();

        await AssignResourceAsync(builder);
        await UpdateWorkOrderOwnershipAsync(builder);
    }

    /// <summary>
    /// Assign Resource (DesignAssociate) with the Appointment
    /// </summary>
    private async Task AssignResourceAsync(AppointmentBuilder builder)
    {
        using var scope = _logger.AddScope(new
        {
            builder.SfServiceAppointmentId,
            builder.SfServiceMemberId,
        });

        _logger.LogInformation("Assign resource to appointment");

        // assign resource (da)
        var resource = new AssignedResource
        {
            ServiceAppointmentId = builder.SfServiceAppointmentId,
            ServiceResourceId = builder.SfServiceMemberId,
            // IsPrimaryResource = true,
        };

        var sfResource = await SalesforceClient.CreateAsync(builder.Token, "AssignedResource", resource);
        if (sfResource.error != null)
        {
            _logger.LogError("Failed to add AssignedResource: {Error}", sfResource.error);
            // throw new Exception($"Failed to add AssignedResource: {sfResource.error}");
            return;
        }

        _logger.LogInformation("Assigned {ServiceResourceId} to ServiceAppointment", resource.ServiceResourceId);
    }

    /// <summary>
    /// Update "Project" (WorkOrder) owner (e.g. Design associate)
    /// FCI only
    /// </summary>
    private async Task UpdateWorkOrderOwnershipAsync(AppointmentBuilder builder)
    {
        using var scope = _logger.AddScope(new
        {
            builder.SfServiceAppointmentId,
            builder.SfWorkOrderId,
            builder.SfServiceMemberId,
            builder.SfUserId,
        });

        _logger.LogInformation("Explicitly assign project to DA");

        var expando = new ExpandoObject();
        var iDict = (IDictionary<string, object>)expando;
        iDict["Design_Associate__c"] = builder.SfServiceMemberId;
        iDict["Design_Associate_User__c"] = builder.SfUserId;

        try
        {
            await SalesforceClient.UpdateAsync(builder.Token, "WorkOrder", builder.SfWorkOrderId, expando);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign project to DA");
        }

        _logger.LogInformation("WorkOrder update worked");
    }

    private class AssignedResource
    {
        public string ServiceAppointmentId { get; set; }

        public string ServiceResourceId { get; set; }
        // public bool IsPrimaryResource { get; set; }
    }

    public class ConvertLeadRequest
    {
        public string LeadId { get; set; }
        public string OwnerId { get; set; }
        public string ServiceTerritoryId { get; set; }
        public string ServiceMemberId { get; set; }
        public string LeadSource { get; set; }
        public string WorkTypeName { get; set; }
    }

    public class ConvertLeadResponse
    {
        public string LeadId { get; set; }
        public string ContactId { get; set; }
        public string AccountId { get; set; }
        public string OpportunityId { get; set; }
        public string WorkOrderId { get; set; }
        public string WorkOrderLineId { get; set; }
        public string Error { get; set; }
    }

    private class SfLead
    {
        [JsonProperty(PropertyName = "id")] public string Id { get; set; }

        [JsonProperty(PropertyName = "convertedAccountId")]
        public string ConvertedAccountId { get; set; }
    }

    private class SfWorkOrder
    {
        [JsonProperty(PropertyName = "id")] public string Id { get; set; }

        // ...
    }

    private class SfWorkOrderLineItem
    {
        [JsonProperty(PropertyName = "id")] public string Id { get; set; }

        // ...
    }

    private class LeadBuilder
    {
        public ExportToSalesforceAction.Message Action { get; init; }
        public IEntityContext Context { get; init; }

        public User User { get; init; }
        public Organization Organization { get; set; }

        public SalesforceToken Token { get; init; }

        public Lead Lead { get; set; }

        private string _leadId;

        public string SfLeadId
        {
            get => _leadId ??= Lead.Integrations?.FirstOrDefault((x => x.IntegrationId == IntegrationIds.Salesforce && x.Tag == "Lead"))?.ExternalId;
            set => _leadId = value;
        }

        private string _accountId;

        public string SfAccountId
        {
            get => _accountId ??= Lead.Integrations?.FirstOrDefault((x => x.IntegrationId == IntegrationIds.Salesforce && x.Tag == "Account"))?.ExternalId;
            set => _accountId = value;
        }

        public string SfUserId => User.Identities?
            .FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.Salesforce) && x.Data != null)?
            .ExternalId;

        public string SfServiceMemberId => User.Identities?
            .FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.Salesforce) && x.Name == "ServiceResource")?
            .ExternalId;

        public string SfTerritoryId => Organization?.Identities?
            .FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.Salesforce))?
            .ExternalId;

        public string SfWorkOrderId { get; set; }

        public string SfWorkOrderLineItemId { get; set; }
    }

    private class AppointmentBuilder : LeadBuilder
    {
        public Appointment Appointment { get; set; }
        public string SfServiceAppointmentId { get; set; }
    }
}