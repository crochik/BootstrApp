using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Crochik.Messaging;
using Crochik.Mongo;
using Crochik.NET.APM;
using CsvHelper;
using Messages.Flow;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Interfaces;

namespace PI.Shared.Services;

public class Headers
{
    public string AuthorizationHash { get; set; }
    public string Origin { get; set; }
    public string Referer { get; set; }
    public string UserAgent { get; set; }
}

public class HttpRequestDetails
{
    public Dictionary<string, object> Query { get; set; }
    public Headers Headers { get; set; } = new Headers();
    public string RemoteIp { get; set; }
}

public class LeadRequest
{
    [BsonId] public ObjectId Id { get; set; }

    public Guid LeadTypeId { get; set; }
    public HttpRequestDetails Request { get; set; }
    public NewLeadResult Response { get; set; }
    public string Body { get; set; }
    public string Error { get; set; }

    public long? ContentLength { get; set; }
    public string ContentType { get; set; }
    public string Host { get; set; }
    public string Method { get; set; }
    public string Path { get; set; }
    public string TraceIdentifier { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedOn { get; set; }

    public IDictionary<string, object> Payload { get; set; }
}

public class NewLeadResult
{
    public Guid LeadId { get; set; }
    public Guid? DefaultAppointmentTypeId { get; set; }
    public Guid? EntityId { get; set; }
    public string ContentType { get; set; }
}

public class LeadBuilderService
{
    private static readonly HashAlgorithm HashAlgo = SHA256.Create();

    private readonly ILogger<LeadBuilderService> _logger;
    // private readonly IAPMService _apmService;
    private readonly MongoConnection _connection;
    private readonly IEntityIdentityAdapter _entityAdapter;
    private readonly ILeadAdapter _leadAdapter;
    private readonly IIntegrationLeadAdapter _integrationLeadAdapter;
    private readonly IValueMapperService _valueMapperService;
    private readonly IMessageBroker _messageBroker;

    public LeadBuilderService(
        ILogger<LeadBuilderService> logger,
        // IAPMService apmService,
        MongoConnection connection,
        IEntityIdentityAdapter entityAdapter,
        ILeadAdapter leadAdapter,
        IIntegrationLeadAdapter integrationLeadAdapter,
        IValueMapperService valueMapperService,
        IMessageBroker messageBroker
    )
    {
        _logger = logger;
        // _apmService = apmService;
        _connection = connection;
        _entityAdapter = entityAdapter;
        _leadAdapter = leadAdapter;
        _integrationLeadAdapter = integrationLeadAdapter;
        _valueMapperService = valueMapperService;
        _messageBroker = messageBroker;
    }

    private async Task<string> ParseIntegrationAsync(LeadBuilder builder)
    {
        if (!builder.LeadType.Settings.IsPartOfIntegration())
        {
            return null;
        }

        // using var apm = _apmService.StartTransaction("Lead", "ParseIntegration");
        var leadType = builder.LeadType;
        var integrationId = leadType.Settings.Integration.IntegrationId.Value;

        foreach (var externalField in leadType.Settings.Integration.ExternalIdFields)
        {
            var externalId = builder.GetResolvedValue(externalField.Name);
            if (string.IsNullOrEmpty(externalId)) continue;

            // add to the list 
            builder.IntegrationLeads ??= new List<LeadBuilder.IntegrationLeadMatch>();

            var lead = default(Lead);
            var ilead = default(IIntegrationLead);

            var list = (await _leadAdapter.GetByIntegrationsAsync(builder.Context, integrationId, externalId))?.ToArray();
            if (list?.Length > 0)
            {
                if (list.Length > 1)
                {
                    // merge?
                    _logger.LogError("More than one lead with the same external id: {ExternalId}", externalId);
                }

                lead = list[0].Item1;
                ilead = list[0].Item2;
            }

            builder.IntegrationLeads.Add(new LeadBuilder.IntegrationLeadMatch
            {
                Tag = externalField.Tag,
                IntegrationId = integrationId,
                ExternalId = externalId,
                Lead = lead,
                IntegrationLead = ilead,
                Settings = externalField
            });
        }

        if (builder.IntegrationLeads == null)
        {
            // none found
            if (leadType.Settings.Integration.IsRequired)
            {
                var fields = string.Join(", ", leadType.Settings.Integration.ExternalIdFields.Select(x => x.Name));
                return $"Missing required {fields}";
            }

            // apm.Context = new
            // {
            //     ExternalId = "Not Provided",
            //     IntegrationId = "Not Provided"
            // };

            return null;
        }

        // check if existing
        LeadBuilder.IntegrationLeadMatch found = null;
        var merged = new HashSet<Guid>();
        foreach (var match in builder.IntegrationLeads)
        {
            if (match.IntegrationLead == null) continue;

            if (found == null)
            {
                found = match;
                continue;
            }

            if (found.Lead.Id == match.Lead.Id) continue;

            if (merged.Contains(match.Lead.Id))
            {
                // update reference
                match.Lead = found.Lead;
                match.IntegrationLead = found.Lead.Find(match.IntegrationId, match.ExternalId);
            }
            else
            {
                merged.Add(found.Lead.Id);
                merged.Add(match.Lead.Id);

                // merge leads
                var error = await MergeAsync(builder.Context, found, match);
                if (error != null) return error;
            }
        }

        var externalIds = string.Join(", ", builder.IntegrationLeads.Select(x => x.ExternalId));

        if (found == null)
        {
            return !leadType.Settings.Integration.CreateIfMissing ? $"Couldn't find integration for lead: {externalIds}" : null;
        }

        if (leadType.Settings.FailIfFound())
        {
            return "Update of existing not allowed";
        }

        merged.Remove(found.Lead.Id);
        builder.MergedLeadIds = merged.ToArray();
        builder.ExistingLead = found.Lead;

        // apm.Context = new
        // {
        //     ExternalIds = externalIds,
        //     IntegrationId = integrationId,
        //     builder.ExistingLeadId
        // };

        return null;
    }

    private async Task<string> MergeAsync(IEntityContext context, LeadBuilder.IntegrationLeadMatch left, LeadBuilder.IntegrationLeadMatch right)
    {
        var entity = default(IEntity);

        if (left.Lead.EntityId != right.Lead.EntityId)
        {
            var e1 = await _entityAdapter.GetEntityByIdAsync(left.Lead.EntityId);
            var e2 = await _entityAdapter.GetEntityByIdAsync(right.Lead.EntityId);
            if (e1.CanAccess(e2))
            {
                entity = e2;
            }
            else if (e2.CanAccess(e1))
            {
                entity = e1;
            }
            else
            {
                _logger.LogError("Can't merge leads from different entities: {LeadId} {OtherLeadId}", left.Lead.Id, right.Lead.Id);
                return null;
            }
        }

        var merged = await MergeAsync(context, left.Lead, right.Lead);
        if (merged == null) return $"Failed to merge {left.Lead.Id} with {right.Lead.Id}";

        right.Lead = left.Lead = merged;
        left.IntegrationLead = left.Lead.Find(left.IntegrationId, left.ExternalId);
        right.IntegrationLead = right.Lead.Find(right.IntegrationId, right.ExternalId);

        if (entity != null && merged.EntityId != entity.Id)
        {
            if (!await MoveToEntityAsync(entity.Context.WithActorFrom(context), entity.Id))
            {
                return $"Error reassigning {merged.Id} to {entity.Id}";
            }
        }

        return null;
    }

    private async Task<bool> MoveToEntityAsync(IEntityContext context, Guid id)
    {
        var result = await _connection.Filter<Lead>()
            .Eq(x => x.Id, id)
            .Update
            .Set(x => x.EntityId, context.EntityId)
            .Set(x => x.EntityIds, context.GetEntityIds())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateOneAsync();

        return result.ModifiedCount == 1;
    }

    /// <summary>
    /// Merge leads. The one with the most recent appointment will be win or the oldest
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    /// <exception cref="Exception"></exception>
    private async Task<Lead> MergeAsync(IEntityContext context, Lead l, Lead r)
    {
        if (l == null || r == null) throw new NotSupportedException("Only support Lead");

        (l, r) = await SortByPriority(l, r);

        var query = _connection.Filter<Lead>()
            .Eq(x => x.Id, l.Id)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor());

        // copy properties
        var modified = new List<string>();
        foreach (var prop in r.AllProperties())
        {
            if (prop.Value == null) continue;
            if (l.Properties.TryGetValue(prop.Key, out var value) && prop.Value.Equals(value)) continue;

            l.Properties[prop.Key] = prop.Value;
            modified.Add(prop.Key);
        }

        if (modified.Count > 0)
        {
            query.ResetUsingProperties(l);
        }

        // copy integrations
        if (MergeIntegrations(l, r))
        {
            query.Set(x => x.Integrations, l.Integrations);
        }

        l = await query.UpdateAndGetOneAsync();
        if (l == null)
        {
            throw new Exception($"Failed to update lead: {l.Id}");
        }

        // mark as replaced
        var result = await _connection.Filter<Lead>()
            .Eq(x => x.Id, r.Id)
            .Update
            .Set(x => x.ReplacedById, l.Id)
            .Set(x => x.IsActive, false)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateOneAsync();

        if (result.ModifiedCount != 1)
        {
            _logger.LogError($"Failed to mark {r.Id} as replaced by {l.Id}");
        }

        // move appointments to other lead
        // shouldn't happen but...
        
        // change parent 
        await _connection.Filter<Appointment>()
            .Eq(x => x.LeadId, r.Id)
            .In(x => x.Parent.ObjectType, [nameof(Lead), null])
            .Update
            .Set(x => x.Parent, new ReferencedObject
            {
                ObjectType = nameof(Lead),
                ObjectId = l.Id,
            })
            .UpdateManyAsync();
        
        // change lead
        await _connection.Filter<Appointment>()
            .Eq(x => x.LeadId, r.Id)
            .Update
            .Set(x => x.LeadId, l.Id)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateManyAsync();
        
        return l;
    }

    private async Task<(Lead, Lead)> SortByPriority(Lead l, Lead r)
    {
        var appt1 = await _connection.Filter<Appointment>()
            .Eq(x => x.LeadId, l.Id)
            .SortAsc(x => x.CreatedOn)
            .FirstOrDefaultAsync();

        var appt2 = await _connection.Filter<Appointment>()
            .Eq(x => x.LeadId, r.Id)
            .SortAsc(x => x.CreatedOn)
            .FirstOrDefaultAsync();

        if (appt1 != null)
        {
            if (appt2 != null)
            {
                if (appt1.CreatedOn > appt2.CreatedOn) SwapLeads();
                _logger.LogWarning("Merging two leads with appts: {LeadId} {OtherLeadId}", l.Id, r.Id);
            }
        }
        else if (appt2 != null)
        {
            SwapLeads();
        }
        else if (l.CreatedOn > r.CreatedOn)
        {
            SwapLeads();
        }

        return (l, r);

        void SwapLeads()
        {
            (l, r) = (r, l);
        }
    }

    private static bool MergeIntegrations(Lead into, Lead from)
    {
        if (from.Integrations == null || from.Integrations.Length < 1)
        {
            // nothing to do
            return false;
        }

        if (into.Integrations == null || into.Integrations.Length < 1)
        {
            // copy
            into.Integrations = from.Integrations;
            return true;
        }

        // combine
        var integrations = new List<LeadIntegration>();
        integrations.AddRange(into.Integrations);

        foreach (var i in from.Integrations)
        {
            var existing = into.Integrations?.FirstOrDefault(x => x.IntegrationId == i.IntegrationId && string.Equals(x.ExternalId, i.ExternalId));
            if (existing == null)
            {
                integrations.Add(i);
                continue;
            }

            if (i.GetLastModified() > existing.LastModifiedOn)
            {
                existing.Data = i.Data ?? existing.Data;
                existing.Url = i.Url ?? existing.Url;
                existing.Status = i.Status ?? existing.Status;
            }
            else
            {
                existing.Data ??= i.Data;
                existing.Url ??= i.Url;
                existing.Status ??= i.Status;
            }

            existing.LastModifiedOn = DateTime.UtcNow;
        }

        into.Integrations = integrations.ToArray();

        return true;
    }

    private async Task<string> ParseLeadIdAsync(LeadBuilder builder)
    {
        if (string.IsNullOrEmpty(builder.LeadType.Settings?.UpdatePolicy?.LeadIdField)) return null;

        var leadIdValue = builder.GetResolvedValue(builder.LeadType.Settings?.UpdatePolicy?.LeadIdField);
        if (string.IsNullOrEmpty(leadIdValue))
        {
            // no value found
            return null;
        }

        // using var apm = _apmService.StartTransaction("Lead", "ParseLeadId");

        if (!Guid.TryParse(leadIdValue, out var existingLeadId))
        {
            return $"Invalid format for {builder.LeadType.Settings?.UpdatePolicy?.LeadIdField}, not GUID";
        }

        if (builder.ExistingLeadId.HasValue && builder.ExistingLeadId.Value != existingLeadId)
        {
            return $"Lead mismatch {builder.ExistingLeadId} != {existingLeadId}";
        }

        // make sure it exists 
        var existing = await _connection.GetByIdAsync<Lead>(existingLeadId);
        if (existing == null)
        {
            // apm.Context = new
            // {
            //     LeadId = existingLeadId,
            //     Result = "NotFound"
            // };

            return $"Lead doesn't exist: {existingLeadId}";
        }

        if (builder.LeadType.Settings.FailIfFound())
        {
            return "Update of existing not allowed";
        }

        // only allow change if it is the same type
        if (existing.LeadTypeId != builder.LeadType.Id)
        {
            // apm.Context = new
            // {
            //     LeadId = existingLeadId,
            //     existing.LeadTypeId,
            //     Result = "InvalidType"
            // };

            return "Can only modify leads of the same type";
        }

        builder.ExistingLead = existing;

        // apm.Context = new { LeadId = existingLeadId };

        return null;
    }

    private async Task<string> UpdateAsync(LeadBuilder builder)
    {
        if (builder.ExistingLead == null) return null;

        // using var apm = _apmService.StartTransaction("Lead", "UpdateExisting");

        var updated = builder.ExistingLead;
        if (!builder.UpdateExisting(updated))
        {
            // apm.Context = new { Result = "DoNotUpdate" };
            return builder.Error;
        }

        updated = await _connection.UpdatePropertiesAsync(builder.Context, updated);
        if (updated == null) return "Failed to update existing record";

        // apm.Context = new
        // {
        //     Result = "Updated",
        //     Fields = string.Join(", ", builder.UpdatedFields)
        // };

        await AddOrUpdateIntegrationsAsync(builder);

        var lead = builder.ExistingLead;
        if (!UpdateCommunicationPreferences(lead)) return null;

        var result = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.Id, lead.Id)
            .Update
            .Set(x => x.CommunicationPreferences, lead.CommunicationPreferences)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            // .Set(x=>x.LastActor, ...)
            .UpdateOneAsync();

        if (result.ModifiedCount != 1)
        {
            _logger.LogError("Failed to update communication preferences for {LeadId}", lead.Id);
        }
        else
        {
            _logger.LogInformation("Communication preferences for {LeadId} updated", lead.Id);
        }

        return null;
    }

    /// <summary>
    /// Check the "standard" properties for opting out and adjust the communication preferences if necessary
    /// </summary>
    private bool UpdateCommunicationPreferences(Lead lead)
    {
        var changed = false;
        foreach (var kvp in lead.AllProperties())
        {
            var channel = default(string);
            switch (kvp.Key)
            {
                case Lead.PropertyName_OptedOutOfEmail:
                    channel = CommunicationChannel.Email;
                    break;
                case Lead.PropertyName_OptedOutOfFax:
                    channel = CommunicationChannel.Fax;
                    break;
                case Lead.PropertyName_OptedOutOfPhone:
                    channel = CommunicationChannel.Phone;
                    break;
                case Lead.PropertyName_OptedOutOfMobile:
                    channel = CommunicationChannel.SMS;
                    break;
                default:
                    continue;
            }

            if (kvp.Value is not bool boolValue)
            {
                if (kvp.Value is not string strValue) continue;
                if (!bool.TryParse(strValue, out boolValue)) continue;
            }

            if (!boolValue) continue;
            if (lead.GetCommunicationPreference(channel) == CommunicationPreference.OptedOut) continue;

            _logger.LogInformation(
                "{LeadId} opted out of {CommunicationPreference}",
                lead.Id,
                channel
            );

            lead.CommunicationPreferences ??= new Dictionary<string, string>();
            lead.CommunicationPreferences[channel] = CommunicationPreference.OptedOut;
            changed = true;
        }

        return changed;
    }

    private async Task AddOrUpdateIntegrationsAsync(LeadBuilder builder)
    {
        if (builder.IntegrationLeads == null) return;

        foreach (var match in builder.IntegrationLeads)
        {
            var createOrUpdate = BuildIntegration(builder, match);
            if (match.IntegrationLead != null)
            {
                // update
                match.IntegrationLead = await _integrationLeadAdapter.PatchAsync(builder.Context, createOrUpdate);
            }
            else
            {
                // add integration to lead
                match.IntegrationLead = await _integrationLeadAdapter.AddAsync(builder.Context, createOrUpdate);
            }

            if (match.IntegrationLead == null)
            {
                // error
                _logger.LogError("Failed to create/update integration for {LeadId}", builder.ExistingLead.Id);
                // ...
            }
        }
    }

    [Obsolete("Obsolete")]
    private static LeadIntegration BuildIntegration(LeadBuilder builder, LeadBuilder.IntegrationLeadMatch match)
    {
        var createOrUpdate = new LeadIntegration
        {
            Tag = match.Tag,
            IntegrationId = match.IntegrationId,
            ExternalId = match.ExternalId,
            LeadId = builder.LeadId,
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow,
        };

        if (match.Settings.SaveData)
        {
            createOrUpdate.Data = !string.IsNullOrEmpty(builder.SerializedBody) ? JsonConvert.DeserializeObject(builder.SerializedBody) : null;

            createOrUpdate.Url = !string.IsNullOrEmpty(match.Settings.UrlField) ? builder.GetResolvedValue(match.Settings.UrlField) : null;

            createOrUpdate.Status = !string.IsNullOrEmpty(match.Settings.StatusField) ? builder.GetResolvedValue(match.Settings.StatusField) : null;
        }

        // HACK until next refactor
        if (string.IsNullOrEmpty(createOrUpdate.Url) &&
            builder.Context.AccountId.HasValue &&
            builder.Context.AccountId.Value == AccountIds.FCI &&
            match.IntegrationId == IntegrationIds.Salesforce
           )
        {
            createOrUpdate.Url = $"https://fcifloors.my.salesforce.com/{match.ExternalId}";
        }

        return createOrUpdate;
    }

    private async Task<string> CreateAsync(LeadBuilder builder)
    {
        if (builder.ExistingLead != null) return null;

        // using var apm = _apmService.StartTransaction("Lead", "Add");

        var iLeads = builder.IntegrationLeads?.Select(x => BuildIntegration(builder, x)).ToArray();

        var lead = builder.Build();
        lead.Integrations = iLeads;
        UpdateCommunicationPreferences(lead);

        var created = await _connection.CreateAsync(builder.Context, lead);
        if (created == null)
        {
            _logger.LogError("Fail to create Lead");
            return "Fail to create Lead";
        }

        // apm.Context = new
        // {
        //     builder.Result.Id,
        // };

        return null;
    }

    private async Task<string> FireEventsAsync(LeadBuilder builder)
    {
        if (!builder.FireEvents) return null;

        var lead = builder.Result;

        if (builder.ExistingLeadId.HasValue)
        {
            // modified
            var modifiedFields = string.Join(", ", builder.UpdatedFields);

            _logger.LogInformation(
                "{LeadId} Updated: {Fields}",
                lead.Id,
                modifiedFields
            );

            var updateEvent = new GenericFlowEvent(lead)
            {
                RefValues = lead.GetRefs().ToList(),
                MetaValues = new Dictionary<string, object>(lead.GetMeta()),
                Actor = builder.Context.Actor(),
                Description = $"Lead Updated: {modifiedFields}",
                EventTypeId = EventIds.OnLeadUpdated, // TODO: should it be the generic update object event
            };

            updateEvent.SetMetaValue("Fields", modifiedFields);

            await _messageBroker.DispatchAsync(updateEvent, FlowObjectEventRoute.Update.GetRoute(lead));

            if (lead.FlowId.HasValue)
            {
                await _messageBroker.DispatchAsync(updateEvent);
            }

            return null;
        }

        var evt = new GenericFlowEvent(lead)
        {
            RefValues = lead.GetRefs().ToList(),
            MetaValues = new Dictionary<string, object>(lead.GetMeta()),
            Actor = builder.Context.Actor(),
            Description = "Lead Created",
            EventTypeId = EventIds.OnLeadCreated, // TODO: should it be the generic create object event
        };

        await _messageBroker.DispatchAsync(evt, FlowObjectEventRoute.Create.GetRoute(lead));

        // created
        if (lead.FlowId.HasValue)
        {
            await _messageBroker.DispatchAsync(evt);
        }

        return null;
    }

    delegate Task<string> StepAsync(LeadBuilder builder);

    public async Task<int> ImportCSVAsync(IEntityContext context, LeadType leadType, Stream stream, bool fireEvents = true)
    {
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, false);

        if (!await csv.ReadAsync())
        {
            throw new BadRequestException("Failed to open");
        }

        if (!csv.ReadHeader())
        {
            throw new BadRequestException("Failed to read header");
        }

        var accountContext = new AccountContext(context.AccountId.Value);

        int count = 0;
        var header = csv.Context.HeaderRecord;
        while (await csv.ReadAsync())
        {
            var dict = new Dictionary<string, string>(csv.Context.Record.Select((x, i) => new KeyValuePair<string, string>(header[i], x)));
            var json = JsonConvert.SerializeObject(dict);

            var result = await AddAsync(accountContext, leadType, json, fireEvents);
            if (result.Failed)
            {
                _logger.LogError("Failed to import lead: {LineNumber} {Row}", csv.Context.RawRow, csv.Context.RawRecord);
                continue;
            }

            _logger.LogInformation("Imported {LeadId}", result.Result.Id);
            count++;
        }

        return count;
    }

    public async Task<LeadBuilder> AddAsync(IEntityContext context, LeadType leadType, string body, bool fireEvents = true)
    {
        var builder = new LeadBuilder(context, leadType, body)
        {
            FireEvents = fireEvents
        };

        // using var apm = _apmService.StartTransaction("Lead", $"Create {builder.LeadType.Name}", builder.LeadType.Name, "Create");
        // apm.Context = new
        // {
        //     builder.EntityId,
        //     LeadTypeId = builder.LeadType.Id
        // };

        var steps = new StepAsync[]
        {
            MapFieldsAsync,
            OverrideEntityIdAsync,
            ParseIntegrationAsync,
            ParseLeadIdAsync,
            UpdateAsync,
            CreateAsync,
            FireEventsAsync
        };

        foreach (var step in steps)
        {
            // var timer = DateTime.UtcNow;
            if (builder.Failed) break;
            builder.Error = await step.Invoke(builder);
        }

        // apm.Context = new
        // {
        //     builder.Error,
        //     builder.Result?.Id,
        // };

        return builder;
    }

    private async Task<string> OverrideEntityIdAsync(LeadBuilder builder)
    {
        var leadType = builder.LeadType;
        // var lead = builder.MutableLead;

        if (leadType.Settings?.OverrideEntityId() != true) return null;

        // using var apm = _apmService.StartTransaction("Lead", "OverrideEntityId");

        var field = leadType.Settings?.EntityIdOverrideField;
        if (string.IsNullOrEmpty(field))
        {
            return $"Missing {nameof(LeadTypeSettings.EntityIdOverrideField)}";
        }

        var value = builder.GetResolvedValue(field);
        if (string.IsNullOrEmpty(value))
        {
            // value not found, fallback to original entityId for lead
            return null;
        }

        if (!Guid.TryParse(value, out var entityId))
        {
            return $"{field} is not a GUID";
        }

        var entity = await _entityAdapter.GetEntityByIdAsync(entityId);
        if (entity == null)
        {
            return $"Entity with id={entityId} not found";
        }

        if (!entity.IsActive)
        {
            _logger.LogInformation("{EntityId} is disabled, do not override", entityId);
            return null;
        }

        _logger.LogInformation("Overriding {EntityId} for {LeadId}", entityId, builder.LeadId);

        if (!builder.OverrideEntity(entity)) return builder.Error;

        // apm.Context = new
        // {
        //     FromEntityId = builder.EntityId,
        //     ToEntityId = entity.Id,
        // };

        return null;
    }

    public LeadFlattener GetMapper(LeadType leadType)
    {
        if (leadType?.Id == null) return null;
        return new LeadFlattener(leadType?.Settings, _valueMapperService);
    }

    private Task<string> MapFieldsAsync(LeadBuilder builder)
    {
        return Task.FromResult(MapFields(builder));
    }

    private string MapFields(LeadBuilder builder)
    {
        // using var apm = _apmService.StartTransaction("Lead", "MapFields");

        var leadType = builder.LeadType;
        var mapping = GetMapper(leadType);

        builder.ParseFields(_logger, mapping, leadType.Settings?.RejectOnValidationError != true);

        if (string.IsNullOrEmpty(builder.Error) && !string.IsNullOrEmpty(leadType.Settings?.CreatedOnOverrideField))
        {
            // try to override createdOn date
            var dateStr = builder.GetResolvedValue(leadType.Settings.CreatedOnOverrideField);
            if (!string.IsNullOrWhiteSpace(dateStr))
            {
                if (DateTime.TryParse(dateStr, out var createdOn))
                {
                    builder.OverrideCreatedOn(createdOn);
                }
            }
        }

        return builder.Error;
    }

    /// <summary>
    /// Build request object from http context
    /// </summary>
    public static LeadRequest BuildLeadRequestObject(HttpContext httpContext)
    {
        var httpRequest = httpContext.Request;
        var leadRequest = new LeadRequest
        {
            Request = new HttpRequestDetails(),
            Path = httpRequest.Path.ToString(),
            Host = httpRequest.Host.ToString(),
            ContentType = httpRequest.ContentType,
            Method = httpRequest.Method,
            ContentLength = httpRequest.ContentLength,
            TraceIdentifier = httpContext.TraceIdentifier
        };

        foreach (var parm in httpRequest.Query)
        {
            leadRequest.Request.Query ??= new Dictionary<string, object>();
            var value = parm.Value.Count == 1 ? (object)parm.Value.ToString() : parm.Value.ToArray();
            leadRequest.Request.Query.Add(parm.Key, value);
        }

        foreach (var header in httpRequest.Headers)
        {
            switch (header.Key)
            {
                case "Referer":
                    leadRequest.Request.Headers.Referer = header.Value.ToString();
                    break;
                case "Origin":
                    leadRequest.Request.Headers.Origin = header.Value.ToString();
                    break;
                case "User-Agent":
                    leadRequest.Request.Headers.UserAgent = header.Value.ToString();
                    break;
                case "Authorization":
                    leadRequest.Request.Headers.AuthorizationHash = HashAuthorization(header.Value.ToString());
                    break;
                case "X-Forwarded-For":
                    leadRequest.Request.RemoteIp = header.Value.ToString();
                    break;
            }
        }

        if (string.IsNullOrEmpty(leadRequest.Request.RemoteIp))
        {
            // fallback if didn't find forward header
            leadRequest.Request.RemoteIp = httpContext.Connection.RemoteIpAddress.ToString();
        }

        return leadRequest;
    }

    private static string HashAuthorization(string header)
    {
        // TODO: add some salt
        // ...
        byte[] bytes = HashAlgo.ComputeHash(Encoding.UTF8.GetBytes(header));
        return Convert.ToBase64String(bytes);
    }
}