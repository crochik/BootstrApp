using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Constants;
using PI.Shared.Models;

namespace PI.Salesforce.MarketingCloud;

[BsonDiscriminator("marketingCloud")]
public class MarketingCloudIntegrationConfiguration: IntegrationConfiguration
{
    public const string ProtectionKey = $"EntityIntegration.{nameof(IntegrationIds.MarketingCloud)}";
    
    public MarketingCloudIntegrationConfiguration()
    {
        IntegrationId = IntegrationIds.MarketingCloud;
    }

    public string Subdomain { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
}