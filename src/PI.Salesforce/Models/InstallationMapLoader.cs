using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.U2;
using PI.Shared.Salesforce.Models;

namespace PI.Salesforce.Models;

public class InstallationMapLoader
{
    public static Dictionary<string, SfCustomSetting> CustomSettings { get; set; } = null;
    public static string GetSetting(string settingId) => settingId != null && CustomSettings.TryGetValue(settingId, out var setting) ? setting.Name : null;

    private readonly ILogger<InstallationMap> _logger;
    private readonly MongoConnection _connection;

    public InstallationMapLoader(ILogger<InstallationMap> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public async Task<InstallationMap> LoadAsync([FromRoute] Guid redirectionId)
    {
        using var scope = _logger.AddScope(new
        {
            ShareId = redirectionId,
        });

        var redirection = await _connection.Filter<ShortLinkRedirection>()
            .Ne(x => x.IsActive, false)
            .Eq(x => x.MetaValues["ObjectType"], SfOption.ObjectTypeName)
            .Eq(x => x.MetaValues["ShareId"], redirectionId)
            .FirstOrDefaultAsync();

        if (redirection == null)
        {
            _logger.LogError("Invalid Redirection");
            throw NotFoundException.New("Invalid Link");
        }

        if (!redirection.MetaValues.TryGetValue("ObjectId", out var id))
        {
            _logger.LogError("Couldn't find ObjectId in meta values");
            throw new BadRequestException("Invalid Share");
        }

        if (!id.TryToParseObjectId(out var optionId))
        {
            throw new BadRequestException("Invalid Object Id");
        }

        return await LoadOptionAsync(new AccountContext(redirection.AccountId), optionId);
    }

    public async Task<InstallationMap> LoadOptionAsync(IEntityContext context, Guid id)
    {
        _logger.LogInformation("Render {OptionId}", id);

        if (CustomSettings == null)
        {
            var customSettings = await _connection.Filter<SalesforceObject<SfCustomSetting>>("salesforce.INET_InternalCustomSettings__c")
                .Eq(x => x.AccountId, AccountIds.FCI)
                .FindAsync();

            CustomSettings = customSettings.ToDictionary(x => x.ExternalId, x => x.Properties);
        }

        var model = new InstallationMap
        {
            Id = id
        };

        var option = await _connection.Filter<SalesforceObject<SfOption>>("salesforce.INET_Option__c")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, model.Id)
            .Ne(x => x.Properties.IsDeleted, true)
            .FirstOrDefaultAsync();

        if (option == null) return model;

        var workOrder = option == null
            ? null
            : await _connection.Filter<SalesforceObject<WorkOrder>>("salesforce.WorkOrder")
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.ExternalId, option.Properties.WorkOrderId)
                .Ne(x => x.Properties.IsDeleted, true)
                .FirstOrDefaultAsync();

        var optionLineItems = await _connection.Filter<SalesforceObject<SfOptionLineItem>>("salesforce.INET_OptionLineItem__c")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Properties.OptionId, option.ExternalId)
            .Ne(x => x.Properties.IsDeleted, true)
            .FindAsync();

        var sections = await _connection.Filter<SalesforceObject<Section>>("salesforce.INET_Section__c")
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

        if (workOrder != null && floorPlan != null) workOrder.Properties.Floorplan = floorPlan.Properties;

        var sectionLineItems = sections.IsEmpty()
            ? null
            : await _connection.Filter<SalesforceObject<SfSectionLineItem>>("salesforce.INET_SectionLineItem__c")
                .Eq(x => x.AccountId, context.AccountId.Value)
                .In(x => x.Properties.SectionId, sections.Select(x => x.ExternalId))
                .Ne(x => x.Properties.IsDeleted, true)
                .FindAsync();

        var materialAssignments = option == null
            ? null
            : await _connection.Filter<SalesforceObject<MaterialAssignment>>("salesforce.INET_MaterialAssignment__c")
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Properties.OptionId, option?.ExternalId)
                .Ne(x => x.Properties.IsDeleted, true)
                .FindAsync();

        var rooms = floorPlan == null
            ? null
            : await _connection.Filter<SalesforceObject<Room>>("salesforce.INET_Room__c")
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Properties.FloorPlanId, floorPlan.ExternalId)
                .Ne(x => x.Properties.IsDeleted, true)
                .FindAsync();

        model.Rooms = rooms?
            .Select(x => x.Properties)
            .OrderBy(x => x.Name)
            .ToArray();

        model.MaterialAssignments = materialAssignments?
            .Select(x => x.Properties)
            .ToArray();

        model.OptionLineItems = optionLineItems?
            .Select(x => x.Properties)
            .OrderBy(x => x.Index)
            .ToArray();

        model.Sections = sections?
            .Select(x => x.Properties)
            .OrderBy(x => x.Name)
            .ToArray();

        var externalLinks = await _connection.Filter<SalesforceObject<SfExternalLink>>("salesforce.INET_ExternalLink__c")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Ne(x => x.Properties.IsDeleted, true)
            .OrBuilder(getExternalLinkQueries().ToArray())
            .FindAsync();

        model.ExternalLinks = externalLinks.Select(x => x.Properties).ToArray();

        var roomSections = sections == null
            ? null
            : await _connection.Filter<SalesforceObject<RoomSection>>("salesforce.INET_RoomSection__c")
                .Eq(x => x.AccountId, context.AccountId.Value)
                .In(x => x.Properties.SectionId, sections.Select(x => x.ExternalId))
                .Ne(x => x.Properties.IsDeleted, true)
                .FindAsync();

        if (model.Rooms != null && roomSections != null)
        {
            foreach (var rs in roomSections)
            {
                rs.Properties.Room = model.Rooms.FirstOrDefault(x => x.ExternalId == rs.Properties.RoomId);
            }

            foreach (var room in model.Rooms)
            {
                room.ExternalLinks = model.ExternalLinks.Where(x => x.ParentRoomId == room.ExternalId).ToArray();
            }
        }

        if (model.Sections != null)
        {
            foreach (var section in model.Sections)
            {
                section.SectionLineItems = sectionLineItems?
                    .Select(x => x.Properties)
                    .Where(x => x.SectionId == section.ExternalId)
                    .OrderBy(x => x.Index)
                    .ToArray();

                section.ExternalLinks = model.ExternalLinks?
                    .Where(x => x.ParentSectionId == section.ExternalId)
                    .ToArray();

                var roomIds = roomSections?
                    .Where(x => x.Properties.SectionId == section.ExternalId)
                    .Select(x => x.Properties.RoomId).ToHashSet() ?? new HashSet<string>();

                section.Rooms = model.Rooms?
                    .Where(x => roomIds.Contains(x.ExternalId))
                    .OrderBy(x => x.Name)
                    .ToArray();
            }
        }

        if (model.MaterialAssignments != null && model.Rooms != null)
        {
            var roomIds = roomSections?
                .Select(x => x.Properties.RoomId).ToHashSet() ?? new HashSet<string>();

            roomIds = model.MaterialAssignments.Select(x => x.RoomId).Except(roomIds).ToHashSet();
            if (roomIds.Count > 0)
            {
                model.OtherRooms = model.Rooms?
                    .Where(x => roomIds.Contains(x.ExternalId))
                    .OrderBy(x => x.Name)
                    .ToArray();
            }
        }

        model.Option = option?.Properties;
        model.WorkOrder = workOrder?.Properties;
        model.ExternalLinks = model.ExternalLinks.Where(x => x.ParentRoomId == null).ToArray();
        return model;

        IEnumerable<Action<Query<SalesforceObject<SfExternalLink>>>> getExternalLinkQueries()
        {
            if (sections != null) yield return q => q.In(x => x.Properties.ParentSectionId, sections.Select(x => x.ExternalId));
            if (model.Rooms != null) yield return q => q.In(x => x.Properties.ParentRoomId, model.Rooms.Select(x => x.ExternalId));
            if (floorPlan != null) yield return q => q.Eq(x => x.Properties.ParentFloorPlanId, floorPlan.ExternalId);
            if (workOrder != null) yield return q => q.Eq(x => x.Properties.ParentProjectId, workOrder.ExternalId);
            
            // not including the external links added to the option 
            // those include the signatures, signed proposal, ...
        }
    }
}