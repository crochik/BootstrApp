using System;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.ProductCatalog.Models
{
    [BsonDiscriminator("Style")]
    public class CatalogStyleOperation : CatalogOperation
    {
        public Guid CatalogFeedId {get;set;}
        public CatalogStyleUpdate Style { get; set; }
        public CatalogItemOperation[] Items { get; set; }
    }
}