using System;
using System.Collections.Generic;
using PI.Shared.Models;

namespace PI.Shared.Data.Models;

public class LeadTypeUpdatePolicy
{
    public static LeadTypeUpdatePolicy Default { get; } = new LeadTypeUpdatePolicy
    {
        UpdateExisting = true
    };

    public bool IgnoreUpdates { get; set; }
    public bool UpdateExisting { get; set; }
    public bool AllowUnsetProperty { get; set; }
    public string LeadIdField { get; set; }
}

public class ExternalReferenceId
{
    public string Name { get; set; }
    public object Value { get; set; }
}

public class LeadTypeIntegrationSettings
{
    public class ExternalField
    {
        public string Name { get; set; }
        public string Tag { get; set; }
        public bool SaveData { get; set; }
        public string UrlField { get; set; }
        public string StatusField { get; set; }
    }

    public Guid? IntegrationId { get; set; }
    public bool IsRequired { get; set; }
    public bool CreateIfMissing { get; set; }

    public ExternalField[] ExternalIdFields { get; set; } = Array.Empty<ExternalField>();
}

public class LeadTypeSettings
{
    public FieldMapperConfig[] Fields { get; set; }
    public bool RejectOnValidationError { get; set; }
    public IEnumerable<string> PostValidation { get; set; }
    public string EntityIdOverrideField { get; set; }
    public string CreatedOnOverrideField { get; set; }
    public LeadTypeUpdatePolicy UpdatePolicy { get; set; } = LeadTypeUpdatePolicy.Default;
    public LeadTypeIntegrationSettings Integration { get; set; }
}

public static class LeadTypeExtensions
{
    public static bool IsPartOfIntegration(this LeadTypeSettings settings)
    {
        return settings != null &&
               settings.Integration?.ExternalIdFields?.Length > 0 &&
               settings.Integration?.IntegrationId != null;
    }

    public static bool OverrideEntityId(this LeadTypeSettings settings)
    {
        return settings != null && !string.IsNullOrEmpty(settings.EntityIdOverrideField);
    }

    public static bool FailIfFound(this LeadTypeSettings settings)
    {
        return settings?.UpdatePolicy != null &&
               !settings.UpdatePolicy.IgnoreUpdates && !settings.UpdatePolicy.UpdateExisting;
    }
}

public class LeadType : FlowObjectModel
{
    public LeadTypeSettings Settings { get; set; }
    public LeadTypeIntegration[] Integrations { get; set; }

    /// <summary>
    /// Initial flow id for leads created by it
    /// </summary>
    public Guid? InitialFlowId { get; set; }

    /// <summary>
    /// Initial object status id for leads created by it
    /// </summary>
    public Guid? InitialObjectStatusId { get; set; }

    /// <summary>
    /// What (Lead) object type to create
    /// </summary>
    public string ObjectType { get; set; }
    
    /// <summary>
    /// Flow Id for inbound leads received (lms v2)
    /// </summary>
    public Guid? TransactionFlowId { get; set; }
    
    /// <summary>
    /// Initial Object Status Id for inbound leads received (lms v2)
    /// </summary>
    public Guid? TransactionObjectStatusId { get; set; }
    
    public LeadType()
    {
    }
}