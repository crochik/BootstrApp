using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace PI.LangChain.Models;

[BsonDiscriminator("claude")]
public class ClaudeIntegrationConfiguration : IntegrationConfiguration
{
    public static readonly string ProtectionKey = "ClaudeIntegrationConfiguration.APIKey";
    
    public string APIKey { get; set; }
}