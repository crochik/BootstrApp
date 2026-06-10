using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.ProductCatalog.Models;

[BsonCollection("fcb2b.ProductCatalog")]
public class ProductCatalog : FlowObjectModel, IExternalId
{
    public string ExternalId { get; set; }
}