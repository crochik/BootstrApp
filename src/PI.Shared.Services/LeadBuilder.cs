using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace PI.Shared.Services;

public class LeadBuilder
{
    public LeadType LeadType { get; }
    public string SerializedBody { get; }
    public List<IntegrationLeadMatch> IntegrationLeads { get; set; }
    public bool Failed => Error != null;

    private string _errorMessage;
    public string Error
    {
        get => _errorMessage;
        set
        {
            if (string.IsNullOrEmpty(_errorMessage))
            {
                _errorMessage = value;
            }
        }
    }

    private Lead _lead = null;
    private Lead MutableLead
    {
        get
        {
            if (_lead == null)
            {
                if (LeadType == null)
                {
                    Error = "Missing LeadType";
                    return null;
                }

                _lead = new Lead
                {
                    Id = Guid.NewGuid(),
                    EntityId = EntityId,
                    LeadTypeId = LeadType.Id,
                    AccountId = Context.AccountId.Value,
                    // Body = Body,
                    // SerializedBody = SerializedBody,
                    CreatedOn = DateTime.UtcNow,

                    // using the FlowId in the LeadType object is wrong, for now just as a fallback to the previous behavior
                    FlowId = LeadType.InitialFlowId ?? LeadType.FlowId,
                    ObjectStatusId = LeadType?.InitialObjectStatusId ?? LeadStatusIds.Initial,
                };
            }

            return _lead;
        }
    }

    public Lead ExistingLead { get; set; }
    public Guid? ExistingLeadId => ExistingLead?.Id;

    public Guid LeadId => ExistingLeadId ?? MutableLead.Id;

    private Lead _result;
    public Lead Result => Failed ? null : _result;

    public string[] UpdatedFields { get; private set; }
    public Guid[] MergedLeadIds { get; set; }

    public Guid EntityId => Context.EntityId ?? throw new Exception("Invalid Context");
    public IEntityContext Context { get; private set; }
    public string IntegratonStatus { get; set; } = "Exported by integration";
    public bool FireEvents { get; set; } = true;

    public LeadBuilder(IEntityContext context, LeadType leadType, string body)
    {
        Context = context;
        LeadType = leadType;
        SerializedBody = body;
    }

    public string GetResolvedValue(string fieldName) => MutableLead[fieldName];

    public void ParseFields(ILogger logger, LeadFlattener mapping, bool skipValidation)
    {
        var body = JsonConvert.DeserializeObject(SerializedBody);

        // inflate
        if (mapping.Mapping != null)
        {
            foreach (var field in mapping.Mapping)
            {
                var value = field.Mapper(field.Config, body, MutableLead);
                value ??= field.Config.DefaultValue;
                if (value == null) continue;
                if (value is string str && string.IsNullOrWhiteSpace(str)) continue;

                MutableLead.SetValue(field.Config.Name, value);
            }
        }

        if (skipValidation || mapping.ValidationRules == null) return;

        foreach (var rule in mapping.ValidationRules)
        {
            var isValid = rule.Validate(logger, mapping.Settings.Fields, MutableLead);
            if (!isValid)
            {
                Error = $"Failed Validation: {rule.Condition}";
            }
        }
    }

    internal bool OverrideEntity(IEntity entity)
    {
        if (_result != null)
        {
            Error = "Can't reassign after done";
            return false;
        }

        Context = entity.Context.WithActorFrom(Context);
        MutableLead.EntityId = entity.Id;

        return true;
    }

    internal bool OverrideCreatedOn(DateTime createdOn)
    {
        MutableLead.CreatedOn = createdOn;
        return true;
    }

    internal bool UpdateExisting(Lead existing)
    {
        if (_result != null) throw new Exception("Can't modify result after set");

        _result = existing;

        // ???
        if (LeadType.Settings.UpdatePolicy.IgnoreUpdates) return false;

        // copy new/modified
        var modified = new List<string>();
        foreach (var prop in MutableLead.AllProperties())
        {
            if (prop.Value == null && !LeadType.Settings.UpdatePolicy.AllowUnsetProperty)
            {
                continue;
            }

            if (!existing.SetValue(prop.Key, prop.Value)) continue;

            modified.Add(prop.Key);
        }

        UpdatedFields = modified.ToArray();

        return true;
    }

    internal Lead Build()
    {
        if (_result != null) throw new Exception("Can only build once");

        _result = MutableLead;

        return MutableLead;
    }

    public class IntegrationLeadMatch
    {
        public Guid IntegrationId { get; set; }
        public string Tag { get; set; }
        public string ExternalId { get; set; }
        public Lead Lead { get; set; }
        public IIntegrationLead IntegrationLead { get; set; }
        public LeadTypeIntegrationSettings.ExternalField Settings { get; set; }
    }
}