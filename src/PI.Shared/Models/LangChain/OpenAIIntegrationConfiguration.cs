using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Constants;
using PI.Shared.Models;

namespace PI.LangChain.Models;

[BsonDiscriminator("openai")]
public class OpenAIIntegrationConfiguration : IntegrationConfiguration
{
    public const string ProtectionKey = $"EntityIntegration.{nameof(IntegrationIds.OpenAI)}";
    
    public OpenAIIntegrationConfiguration()
    {
        IntegrationId = IntegrationIds.OpenAI;
    }
    
    public string APIKey { get; set; }
}