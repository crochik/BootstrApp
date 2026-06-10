using System;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Constants;
using PI.Shared.Models;

namespace PI.CompanyCam.Models;

public enum AutomaticReaction
{
    Always, // always
    Never, // never
    Conditional, // based on status
}

[BsonDiscriminator("companyCam")]
public class CCIntegrationConfiguration : IntegrationConfiguration
{
    public const string ProtectionKey = $"EntityIntegration.{nameof(IntegrationIds.CompanyCam)}";
    
    public CCIntegrationConfiguration()
    {
        IntegrationId = IntegrationIds.CompanyCam;
    }
    
    public string CompanyId { get; set; }
    public string CompanyName { get; set; }
    
    public Token Token { get; set; }
    public string SigningSecret { get; set; }
    public string PersonalAccessToken { get; set; }
    
    public string WebhookId { get; set; }
    
    public Guid? EventFlowId { get; set; }
    public Guid? EventObjectStatusId { get; set; }

    /// <summary>
    /// whether to create CC projects automatically
    /// here just for documentation, it is used in the flow
    /// </summary>
    public AutomaticReaction AutoCreateProject { get; set; } = AutomaticReaction.Always;

    /// <summary>
    /// whether to archive CC projects automatically
    /// here just for documentation, it is used in the flow
    /// </summary>
    public AutomaticReaction AutoArchiveProject { get; set; } = AutomaticReaction.Never;
}