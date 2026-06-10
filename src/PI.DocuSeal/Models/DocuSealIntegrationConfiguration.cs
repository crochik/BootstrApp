using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Constants;
using PI.Shared.Models;

namespace PI.DocuSeal.Models;

[BsonDiscriminator("docuSeal")]
public class DocuSealIntegrationConfiguration : IntegrationConfiguration
{
    public const string ProtectionKey = $"EntityIntegration.{nameof(IntegrationIds.DocuSeal)}";
    
    public DocuSealIntegrationConfiguration()
    {
        IntegrationId = IntegrationIds.DocuSeal;
    }
    
    public Guid? EventFlowId { get; set; }
    public Guid? EventObjectStatusId { get; set; }
}