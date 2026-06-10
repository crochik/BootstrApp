using System;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Constants;

namespace PI.Shared.Models;

[BsonDiscriminator("o365")]
public class O365IntegrationConfiguration : IntegrationConfiguration
{
    // public const string ProtectionKey = "EntityIntegration.O365";
    
    public O365IntegrationConfiguration()
    {
        IntegrationId = IntegrationIds.Office365;
    }
    
    /// <summary>
    /// Whether to capture the body for email messages
    /// </summary>
    public bool CaptureBody { get; set; }
    
    /// <summary>
    /// Whether to automatically summarize the body (to be used as part of the flow)
    /// </summary>
    public string[] SummarizeFor { get; set; }
    
    /// <summary>
    /// Flow for messages received/sent
    /// </summary>
    public Guid? MessageFlowId { get; set; }
    
    /// <summary>
    /// Initial status for messages received/sent
    /// </summary>
    public Guid? MessageObjectStatusId { get; set; }
    
    /// <summary>
    /// Whether to export appointments for the entity
    /// </summary>
    public bool ExportAppointments { get; set; }
}
