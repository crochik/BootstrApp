using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Constants;
using PI.Shared.Models;

namespace PI.Openphone.Models;

[BsonDiscriminator("openPhone")]
public class OpenPhoneIntegrationConfiguration : IntegrationConfiguration
{
    public const string ProtectionKey = $"EntityIntegration.{nameof(IntegrationIds.OpenPhone)}";
    
    public OpenPhoneIntegrationConfiguration()
    {
        IntegrationId = IntegrationIds.OpenPhone;
    }
    
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string SigningSecret { get; set; }
    
    public Dictionary<string, OpenPhoneEventConfig> Events { get; set; }
}

public class OpenPhoneEventConfig
{
    public string ObjectType { get; set; }
    public Guid? FlowId { get; set; }
    public Guid? ObjectStatusId { get; set; }
}