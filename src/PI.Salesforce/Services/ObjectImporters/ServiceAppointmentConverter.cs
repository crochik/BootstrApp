using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Models.Interfaces;
using PI.Shared.Services;

namespace Services;

/*
    "ServiceAppointment" : {
        "_t" : "AppointmentStreamConfig",
        "Name" : "ServiceAppointment",
        "IntegrationId" : "a2a0b3d8-ae75-47a9-8f10-0bb20af9802f",
        "ExternalProvider" : "Salesforce",
        "ExternalIdField" : "id",
        "LeadExternalIdField" : "accountId",
        "EntityExternalIdFields" : [
            "designAssociateC",
            "serviceTerritoryId"
        ],
        "UrlField" : "dynamicSessionLinkC",
        "StartField" : "schedStartTime",
        "EndField" : "schedEndTime",
        "CreatedOnField" : "createdDate",
        "AppointmentTypeId" : "adf59834-9cca-47cb-bdbf-53be06b76b99",
        "InactiveConditions" : [
            {
                "FieldName" : "isDeleted",
                "Value" : true
            },
            {
                "FieldName" : "isActive",
                "Value" : false
            },
            {
                "FieldName" : "status",
                "Value" : "Canceled"
            }
        ],
        "LeadTypeId" : "c691fc87-2768-406d-808a-dc7a2f4e05e0"
    }
*/
public class ServiceAppointmentConverter : AbstractObjectImporter<Appointment>
{
    private const string TargetObjectTypeName = "Appointment";

    private static readonly Guid LeadTypeId = Guid.Parse("c691fc87-2768-406d-808a-dc7a2f4e05e0");
    private static readonly Guid AppointmentTypeId = Guid.Parse("adf59834-9cca-47cb-bdbf-53be06b76b99");

    public override string SourceObjectTypeName => "sf_ServiceAppointment";

    public override string CollectionName => "Appointment";

    public LeadType LeadType { get; private set; }

    public ServiceAppointmentConverter(ILogger<ServiceAppointmentConverter> logger, MongoConnection connection, ObjectTypeService objectTypeService) :
        base(logger, connection, objectTypeService)
    {
    }

    protected override async Task ValidateAsync(IEntityContext context)
    {
        await base.ValidateAsync(context);

        LeadType = await _connection.Filter<LeadType>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, LeadTypeId)
            .FirstOrDefaultAsync();

        if (LeadType == null) throw new NotFoundException($"LeadType not found");
    }

    protected override Task<Appointment> GetAsync(IEntityContext context, SalesforceCustomObject row)
    {
        return _connection.Filter<Appointment>(CollectionName)
            .Eq(x => x.AccountId, context.AccountId.Value)
            .ElemMatchBuilder(
                x => x.Integrations,
                q => q
                    .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
                    .Eq(x => x.ExternalId, row.ExternalId)
            )
            .FirstOrDefaultAsync();
    }

    protected override async ValueTask<WriteModel<Appointment>> AddAsync(IEntityContext context, SalesforceCustomObject src)
    {
        var result = await UpsertAsync(context, src, null);
        return result;
    }

    protected override async ValueTask<WriteModel<Appointment>> UpdateAsync(IEntityContext context, SalesforceCustomObject src, Appointment dst)
    {
        var result = await UpsertAsync(context, src, dst);
        return result;
    }

    private async ValueTask<WriteModel<Appointment>> UpsertAsync(IEntityContext context, SalesforceCustomObject src, Appointment dst, bool createLeadIfMissing = false)
    {
        using var scope = _logger.AddScope(new
        {
            src.ObjectType,
            src.ExternalId,
            TargetObjectTypeName,
            ExistingId = dst?.Id,
        });

        _logger.LogInformation("Calculate Upsert");

        var isActive = true; // GetRequired<bool>(src, "IsActive");
        var status = GetRequired<string>(src, "Status");

        if (!src.TryGetProperty<bool>("IsDeleted", out var isDeleted)) isDeleted = false;
        if (isDeleted) isActive = false;
        if (status == "Canceled") isActive = false;

        if (!src.TryGetProperty<string>("AccountId", out var accountId) || string.IsNullOrWhiteSpace(accountId))
        {
            _logger.LogError("Missing AccountId");
            return null;
        }

        if (!src.TryGetProperty<DateTime?>("SchedStartTime", out var startDate) || !startDate.HasValue)
        {
            _logger.LogInformation("Missing SchedStartTime, ignore for now...");
            return null;
        }

        if (!src.TryGetProperty<DateTime?>("SchedEndTime", out var endDate) || !endDate.HasValue)
        {
            _logger.LogInformation("Missing SchedEndTime, ignore for now...");
            return null;
        }

        if (!src.TryGetProperty<DateTime?>("LastModifiedDate", out var lastModifiedDate) || !lastModifiedDate.HasValue)
        {
            // should never happen but...
            lastModifiedDate = DateTime.UtcNow;
        }

        // hack to preserve format ????
        var data = BsonDocument.Parse(JsonConvert.SerializeObject(src.Properties));

        if (dst != null)
        {
            _logger.LogInformation("Update Existing");
            return getUpdateModifiedFieldsModel();
        }

        // import ServiceAppointment into Appointment 
        _logger.LogInformation("Create (via upsert)");

        // -----
        // FCI specific 
        if (!src.TryGetProperty<string>("Design_Associate__c", out var designAssociate) || string.IsNullOrWhiteSpace(designAssociate))
        {
            _logger.LogInformation("Missing Design Associated, ignore for now...");
            return null;
        }

        var user = await GetAsync<User>(context, designAssociate);
        if (user == null)
        {
            _logger.LogError("Couldn't find Design Associate: {UserExternalId}", designAssociate);
            return null;
        }
        // -----

        var organization = default(Organization);
        if (src.TryGetProperty<string>("ServiceTerritoryId", out var serviceTerritoryId) && !string.IsNullOrWhiteSpace(serviceTerritoryId))
        {
            organization = await GetAsync<Organization>(context, serviceTerritoryId);
        }

        if (!src.TryGetProperty<DateTime?>("CreatedDate", out var createdDate) || !createdDate.HasValue)
        {
            // should never happen but...
            createdDate = DateTime.UtcNow;
        }

        var lead = await GetLeadAsync(context, accountId);
        if (lead == null)
        {
            if (createLeadIfMissing)
            {
                _logger.LogInformation("Couldn't find lead for {AccountId}, create place holder", accountId);
                lead = await createLeadAsync();
            }
            else
            {
                _logger.LogError("Missing Lead, can't create appointment");
                return null;
            }
        }

        var tool = "Other";
        var creator = default(Entity);
        if (src.TryGetProperty<string>("CreatedById", out var createdBy) && !string.IsNullOrWhiteSpace(createdBy))
        {
            creator = await GetEntityAsync(context, createdBy);

            if (creator?.GroupMembership?.Length > 0)
            {
                var group = await _connection.Filter<EntityGroup>()
                    .Eq(x => x.AccountId, creator.AccountId)
                    .In(x => x.Id, creator.GroupMembership)
                    .FirstOrDefaultAsync();

                if (group != null)
                {
                    tool = group.Name;
                }
            }
        }

        var entityId = user.Id;
        var creatorId = creator?.Id ?? user.Id;

        var objectType = await _objectTypeService.GetAsync(context, TargetObjectTypeName);
        if (objectType == null) throw new NotFoundException($"{TargetObjectTypeName} not found");

        // NOTE: it will not update integration.data if it ends up doing an update
        // ...

        var local = AppointmentMetaData.Get(startDate.Value, user.TimeZoneId, createdDate.Value);

        var query = _connection.Filter<Appointment>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .ElemMatchBuilder(
                    x => x.Integrations,
                    q => q
                        .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
                        .Eq(x => x.ExternalId, src.ExternalId)
                )
                .Update
                .SetOnInsert(x => x.AccountId, context.AccountId.Value)
                .SetOnInsert(x => x.Id, Guid.NewGuid())
                .SetOnInsert(x => x.EntityId, entityId)
                .SetOnInsert(x => x.OrganizationId, organization?.Id ?? user.OrganizationId)
                .SetOnInsert(x => x.LeadId, lead.Id)
                .SetOnInsert(x => x.AppointmentTypeId, AppointmentTypeId)
                .SetOnInsert(x => x.CreatedBy, creatorId)
                .SetOnInsert(x => x.CreatorId, creatorId)
                .SetOnInsert(x => x.CreatedOn, createdDate.Value)
                .SetOnInsert(x => x.FlowId, objectType.InitialFlowId)
                .SetOnInsert(x => x.ObjectStatusId, objectType.InitialObjectStatusId)
                .SetOnInsert(x => x.Tool, tool)
                .SetOnInsert(x => x.Tags, new[] { "Salesforce" })
                .SetOnInsert(x => x.Integrations, new[]
                {
                    new AppointmentIntegration
                    {
                        IntegrationId = IntegrationIds.Salesforce,
                        ExternalId = src.ExternalId,
                        Data = data,
                        Status = isActive ? "Scheduled" : "Cancelled",
                        // Url = 
                    }
                })
                .Set(x => x.Start, startDate.Value)
                .Set(x => x.End, endDate.Value)
                .Set(x => x.CancelledOn, !isActive ? lastModifiedDate : (DateTime?)null)
                .Set(x => x.LastActor, context.Actor())
                .Set(x => x.LastModifiedOn, DateTime.Now)
                .Set(x => x.IsActive, isActive)
                .Set(x => x.LocalDate, local.LocalDateStr)
                .Set(x => x.LocalTime, local.LocalTimeStr)
                .Set(x => x.TimeZoneId, user.TimeZoneId)
                .Set(x => x.Name, lead.Name)
                .Set(x => x.Subject, lead.Name)
                .Set(x => x.Description, $"{lead.Name} with {user.Name} on {local.LocalDateStr} at {local.LocalTimeStr}")
                .Set(x => x.Refs["sf_ServiceAppointment"], src.ExternalId)
                .Set(x => x.Refs["sf_ServiceTerritory"], serviceTerritoryId)
                .Set(x => x.Refs["sf_Account"], accountId)
            ;

        // if (local.Tags?.Length > 0)
        // {
        //     query.AddToSetEach(x=>x.Tags, local.Tags);
        // }

        var parentObject = new ReferencedObject
        {
            ObjectId = lead.Id,
            ObjectType = nameof(Lead),
        };
        
        if (src.TryGetProperty<string>("ParentRecordType", out var parentRecordType) && src.TryGetProperty<string>("ParentRecordId", out var parentRecordId))
        {
            query.Set(x => x.Refs[$"sf_{parentRecordType}"], parentRecordId);

            var workOrderExternalId = await GetWorkOrderExternalIdAsync(context, src);
            if (!string.IsNullOrWhiteSpace(workOrderExternalId))
            {
                query.Set(x => x.Refs["sf_WorkOrder"], workOrderExternalId);

                // make project the parent
                parentObject = new ReferencedObject
                {
                    ObjectType = "salesforce.WorkOrder",
                    ObjectId = workOrderExternalId,
                };
            }
        }

        // set parent 
        query.SetOnInsert(x => x.Parent, parentObject);

        var upsertModel = query.UpdateOneModel(true);
        return upsertModel;

        async Task<Lead> createLeadAsync()
        {
            // TODO: replace with a upsert?
            // ...
            var lead = new Lead
            {
                Id = Guid.NewGuid(),
                AccountId = context.AccountId.Value,
                Name = $"Appointment:{src.ExternalId}",
                EntityId = organization?.Id ?? context.AccountId.Value,
                CreatedOn = DateTime.UtcNow,
                LastActor = context.Actor(),
                LeadTypeId = LeadTypeId,

                // using the FlowId in the LeadType object is wrong, for now just as a fallback to the previous behavior
                FlowId = LeadType.InitialFlowId ?? LeadType.FlowId,
                ObjectStatusId = LeadType.InitialObjectStatusId ?? LeadStatusIds.Initial,

                Integrations = new[]
                {
                    new LeadIntegration
                    {
                        IntegrationId = IntegrationIds.Salesforce,
                        ExternalId = accountId,
                        Tag = "Account",
                        CreatedOn = DateTime.UtcNow,
                        Status = "Not Loaded"
                    }
                }
            };

            lead = await _connection.InsertAsync(lead);
            if (lead == null) throw new Exception("Failed to create lead");
            return lead;
        }

        WriteModel<Appointment> getUpdateModifiedFieldsModel()
        {
            using var scope = _logger.AddScope(new
            {
                AppointmentId = dst.Id,
                dst.LeadId
            });

            var query = _connection.Filter<Appointment>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, dst.Id)
                .ElemMatchBuilder(
                    x => x.Integrations,
                    q => q
                        .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
                        .Eq(x => x.ExternalId, src.ExternalId)
                )
                .Update
                .Set($"{nameof(Appointment.Integrations)}.$.{nameof(AppointmentIntegration.Data)}", data);

            var modifiedFields = new Dictionary<string, object>();
            if (isActive != dst.IsActive)
            {
                query.Set(x => x.IsActive, isActive);
                modifiedFields.Add(nameof(Appointment.IsActive), isActive);
            }

            if (startDate.Value != dst.Start)
            {
                query.Set(x => x.Start, startDate.Value);
                modifiedFields.Add(nameof(Appointment.Start), startDate.Value);
            }

            if (endDate.Value != dst.End)
            {
                query.Set(x => x.End, endDate.Value);
                modifiedFields.Add(nameof(Appointment.End), endDate.Value);
            }

            _logger.LogInformation("Modified {ModifiedFields}", string.Join(", ", modifiedFields));

            return query.UpdateOneModel();
        }
    }

    public async Task<Appointment> UpdateAppointmentRefsAsync(IEntityContext context, SalesforceCustomObject source, Appointment appointment)
    {
        if (appointment.Refs?.TryGetStrParam("sf_WorkOrder", out var workOrderExternalId) ?? false)
        {
            // nothing else to do,
            return appointment;
        }

        workOrderExternalId = await GetWorkOrderExternalIdAsync(context, source);
        if (string.IsNullOrWhiteSpace(workOrderExternalId))
        {
            // nothing (yet)
            return appointment;
        }

        var query = _connection.Filter<Appointment>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, appointment.Id)
                .Update
                .Set(x => x.Refs["sf_ServiceAppointment"], source.ExternalId)
                .Set(x => x.Refs["sf_WorkOrder"], workOrderExternalId)
                .Set(x=>x.Parent, new ReferencedObject
                {
                    ObjectType = "salesforce.WorkOrder",
                    ObjectId = workOrderExternalId,
                })
            // TODO: update lastmodified? 
            // .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            // .Set(x => x.LastActor, context.Actor())
            ;

        if (source.TryGetProperty<string>("AccountId", out var accountId))
        {
            query.Set(x => x.Refs["sf_Account"], accountId);
        }

        if (source.TryGetProperty<string>("ServiceTerritoryId", out var serviceTerritoryId))
        {
            query.Set(x => x.Refs["sf_ServiceTerritory"], serviceTerritoryId);
        }

        if (source.TryGetProperty<string>("ParentRecordType", out var parentRecordType) && source.TryGetProperty<string>("ParentRecordId", out var parentRecordId))
        {
            query.Set(x => x.Refs[$"sf_{parentRecordType}"], parentRecordId);
        }

        var updatedAppointment = await query.UpdateAndGetOneAsync();
        if (updatedAppointment == null)
        {
            _logger.LogError("Failed to update {AppointmentId} references", appointment.Id);
            return appointment;
        }

        _logger.LogInformation("Updated references for {AppointmentId}: {WorkOrderExternalId}", appointment.Id, workOrderExternalId);

        // TODO: fire event?
        // ...

        return updatedAppointment;
    }

    private async Task<string> GetWorkOrderExternalIdAsync(IEntityContext context, SalesforceCustomObject src)
    {
        if (!src.TryGetProperty<string>("ParentRecordType", out var parentRecordType) || !src.TryGetProperty<string>("ParentRecordId", out var parentRecordId))
        {
            _logger.LogError("Missing parent object properties for appointment");
            return null;
        }

        if (parentRecordType != "WorkOrderLineItem")
        {
            _logger.LogInformation("Unexpected {ParentObject}", parentRecordType);
            return null;
        }

        // TODO: load objectType and cache it instead 
        // ...
        var woli = await _connection.Filter<SalesforceCustomObject>($"salesforce.WorkOrderLineItem")
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.ExternalId, parentRecordId)
            .FirstOrDefaultAsync();

        if (woli != null && woli.TryGetProperty<string>("WorkOrderId", out var workOrderExternalId))
        {
            _logger.LogInformation("Found Parent {WorkOrderLineItemId}({WorkOrderLineItemExternalId}) - {WorkOrderExternalId} ", woli.Id, woli.ExternalId, workOrderExternalId);
            return workOrderExternalId;
        }

        _logger.LogInformation("Couldn't find {WorkOrderLineItemExternalId}", parentRecordId);
        return null;
    }

    private Task<TEntity> GetAsync<TEntity>(IEntityContext context, string externalId) where TEntity : Entity
    {
        return _connection.Filter<Entity, TEntity>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .ElemMatchBuilder(
                x => x.Identities,
                q => q
                    .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Salesforce))
                    .Eq(x => x.ExternalId, externalId)
            )
            .FirstOrDefaultAsync();
    }

    private Task<Entity> GetEntityAsync(IEntityContext context, string externalId)
    {
        return _connection.Filter<Entity>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .ElemMatchBuilder(
                x => x.Identities,
                q => q
                    .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Salesforce))
                    .Eq(x => x.ExternalId, externalId)
            )
            .FirstOrDefaultAsync();
    }

    private Task<Lead> GetLeadAsync(IEntityContext context, string externalId)
    {
        return _connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .ElemMatchBuilder(
                x => x.Integrations,
                q => q
                    .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
                    .Eq(x => x.ExternalId, externalId)
            )
            .FirstOrDefaultAsync();
    }

    private async Task<Lead> GetLeadAsync(IEntityContext context, Guid id)
    {
        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (lead == null) throw NotFoundException.New<Lead>(id);
        return lead;
    }
}