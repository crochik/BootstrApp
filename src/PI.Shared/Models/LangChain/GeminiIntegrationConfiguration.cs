using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace PI.LangChain.Models;

[BsonDiscriminator("gemini")]
public class GeminiIntegrationConfiguration : IntegrationConfiguration
{
    public static readonly string ProtectionKey = "GeminiIntegrationConfiguration.APIKey";

    public string APIKey { get; set; }
}