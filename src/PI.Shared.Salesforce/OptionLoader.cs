using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Salesforce.Models;

namespace PI.Shared.Salesforce;

public class OptionLoader
{
    private readonly ILogger<OptionLoader> _logger;
    private readonly MongoConnection _connection;

    public OptionLoader(ILogger<OptionLoader> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public async Task<LoadedOption> LoadOptionAsync(IEntityContext context, Guid id)
    {
        _logger.LogInformation("Render {OptionId}", id);

        var option = await _connection.Filter<SalesforceObject<SfOption>>("salesforce.INET_Option__c")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, id)
            .Ne(x => x.Properties.IsDeleted, true)
            .FirstOrDefaultAsync();

        if (option == null) return null;

        var workOrder = await _connection.Filter<SalesforceWorkOrderObject>("salesforce.WorkOrder")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.ExternalId, option.Properties.WorkOrderId)
            .Ne(x => x.Properties.IsDeleted, true)
            .FirstOrDefaultAsync();

        var optionLineItems = await _connection.Filter<SalesforceObject<SfOptionLineItem>>("salesforce.INET_OptionLineItem__c")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Properties.OptionId, option.ExternalId)
            .Ne(x => x.Properties.IsDeleted, true)
            .FindAsync();

        var sections = await _connection.Filter<SalesforceObject<SfSection>>("salesforce.INET_Section__c")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Properties.OptionId, option.ExternalId)
            .Ne(x => x.Properties.IsDeleted, true)
            .FindAsync();

        var floorPlan = workOrder?.Properties?.FloorPlanId == null
            ? null
            : await _connection.Filter<SalesforceObject<SfFloorPlan>>("salesforce.INET_FloorPlan__c")
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.ExternalId, workOrder.Properties.FloorPlanId)
                .Ne(x => x.Properties.IsDeleted, true)
                .FirstOrDefaultAsync();

        var sectionLineItems = sections.IsEmpty()
            ? null
            : await _connection.Filter<SalesforceObject<SfSectionLineItem>>("salesforce.INET_SectionLineItem__c")
                .Eq(x => x.AccountId, context.AccountId.Value)
                .In(x => x.Properties.SectionId, sections.Select(x => x.ExternalId))
                .Ne(x => x.Properties.IsDeleted, true)
                .FindAsync();

        var materialAssignments = await _connection.Filter<SalesforceObject<SfMaterialAssignment>>("salesforce.INET_MaterialAssignment__c")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Properties.OptionId, option?.ExternalId)
            .Ne(x => x.Properties.IsDeleted, true)
            .FindAsync();

        var rooms = floorPlan == null
            ? null
            : await _connection.Filter<SalesforceObject<SfRoom>>("salesforce.INET_Room__c")
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Properties.FloorPlanId, floorPlan.ExternalId)
                .Ne(x => x.Properties.IsDeleted, true)
                .FindAsync();

        var externalLinks = await _connection.Filter<SalesforceObject<SfExternalLink>>("salesforce.INET_ExternalLink__c")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Ne(x => x.Properties.IsDeleted, true)
            .OrBuilder(getExternalLinkQueries().ToArray())
            .FindAsync();

        var roomSections = await _connection.Filter<SalesforceObject<SfRoomSection>>("salesforce.INET_RoomSection__c")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .In(x => x.Properties.SectionId, sections.Select(x => x.ExternalId))
            .Ne(x => x.Properties.IsDeleted, true)
            .FindAsync();

        var lead = default(Lead);
        if (!workOrder.LeadId.HasValue)
        {
            // try to find
            lead = await _connection.Filter<Lead>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .ElemMatchBuilder(x => x.Integrations, q => q
                    .Eq(x => x.IntegrationId, IntegrationIds.Salesforce)
                    .In(x => x.ExternalId, new[]
                    {
                        workOrder.Properties.CustomerId,
                        workOrder.Properties.LeadId,
                    })
                )
                .FirstOrDefaultAsync();

            if (lead != null)
            {
                workOrder.LeadId = lead.Id;
                workOrder.EntityId = lead.EntityId;
            }
        }

        var model = new LoadedOption
        {
            Id = option.Id,
            AccountId = option.AccountId,
            EntityId = workOrder.EntityId, // option is not updated as now
            LeadId = workOrder.LeadId,
            Lead = lead,

            Rooms = rooms?
                .Select(x => x.Properties)
                .OrderBy(x => x.Name)
                .ToArray(),
            MaterialAssignments = materialAssignments?
                .Select(x => x.Properties)
                .ToArray(),
            OptionLineItems = optionLineItems?
                .Select(x => x.Properties)
                .OrderBy(x => x.Index)
                .ToArray(),
            Sections = sections?
                .Select(x => x.Properties)
                .OrderBy(x => x.Name)
                .ToArray(),
            Option = option.Properties,
            WorkOrder = workOrder.Properties,
            RoomSections = roomSections?.Select(x => x.Properties).ToArray(),
            SectionLineItems = sectionLineItems?.Select(x => x.Properties).ToArray(),
            ExternalLinks = externalLinks.Select(x => x.Properties).ToArray(),
            FloorPlan = floorPlan?.Properties,
        };

        return model;

        IEnumerable<Action<Query<SalesforceObject<SfExternalLink>>>> getExternalLinkQueries()
        {
            if (workOrder != null) yield return q => q.Eq(x => x.Properties.ParentProjectId, workOrder.ExternalId);
            if (floorPlan != null) yield return q => q.Eq(x => x.Properties.ParentFloorPlanId, floorPlan.ExternalId);
            if (sections != null) yield return q => q.In(x => x.Properties.ParentSectionId, sections.Select(x => x.ExternalId));
            if (rooms != null) yield return q => q.In(x => x.Properties.ParentRoomId, rooms.Select(x => x.ExternalId));
            yield return q => q.Eq(x => x.Properties.OptionId, option.ExternalId);
        }
    }
}

// if (workOrder != null && floorPlan != null) workOrder.Properties.Floorplan = floorPlan.Properties;

// if (model.Rooms != null && roomSections != null)
// {
//     foreach (var rs in roomSections)
//     {
//         rs.Properties.Room = model.Rooms.FirstOrDefault(x => x.Id == rs.Properties.RoomId);
//     }
//
//     foreach (var room in model.Rooms)
//     {
//         room.ExternalLinks = model.ExternalLinks.Where(x => x.ParentRoomId == room.Id).ToArray();
//     }
// }

// if (model.Sections != null)
// {
//     foreach (var section in model.Sections)
//     {
//         section.SectionLineItems = sectionLineItems?
//             .Select(x => x.Properties)
//             .Where(x => x.SectionId == section.Id)
//             .OrderBy(x => x.Index)
//             .ToArray();
//
//         section.ExternalLinks = model.ExternalLinks?
//             .Where(x => x.ParentSectionId == section.Id)
//             .ToArray();
//
//         var roomIds = roomSections?
//             .Where(x => x.Properties.SectionId == section.Id)
//             .Select(x => x.Properties.RoomId).ToHashSet() ?? new HashSet<string>();
//
//         section.Rooms = model.Rooms?
//             .Where(x => roomIds.Contains(x.Id))
//             .OrderBy(x => x.Name)
//             .ToArray();
//     }
// }

// if (model.MaterialAssignments != null && model.Rooms != null)
// {
//     var roomIds = roomSections?
//         .Select(x => x.Properties.RoomId).ToHashSet() ?? new HashSet<string>();
//
//     roomIds = model.MaterialAssignments.Select(x => x.RoomId).Except(roomIds).ToHashSet();
//     if (roomIds.Count > 0)
//     {
//         model.OtherRooms = model.Rooms?
//             .Where(x => roomIds.Contains(x.Id))
//             .OrderBy(x => x.Name)
//             .ToArray();
//     }
// }

public class LoadedOption
{
    public SfOption Option { get; set; }
    public SfRoom[] Rooms { get; set; }
    public SfMaterialAssignment[] MaterialAssignments { get; set; }
    public SfOptionLineItem[] OptionLineItems { get; set; }
    public SfSection[] Sections { get; set; }
    public SfExternalLink[] ExternalLinks { get; set; }
    public SfWorkOrder WorkOrder { get; set; }
    public SfRoomSection[] RoomSections { get; set; }
    public SfSectionLineItem[] SectionLineItems { get; set; }
    public SfFloorPlan FloorPlan { get; set; }
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid EntityId { get; set; }
    public Guid? LeadId { get; set; }
    public Lead Lead { get; set; }
}