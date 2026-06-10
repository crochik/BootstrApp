using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.ProductCatalog.Models
{
    [BsonDiscriminator("Item")]
    // [PropertyNickName("Changes|Name", nameof(Changes), nameof(PropertyUpdate.Name))]
    // [PropertyNickName("MergedItem|SKU", nameof(MergedItem), nameof(CatalogItem.SKU))]
    // [PropertyNickName(nameof(CatalogItem.CatalogFeedId), nameof(MergedItem), nameof(CatalogItem.CatalogFeedId))]
    public class CatalogItemOperation : CatalogOperation
    {
        public CatalogItemUpdate Update { get; set; }
        public CatalogItem MergedItem { get; set; }
        public Dictionary<string, object> OverriddenProps { get; set; }
        public PropertyUpdate[] Changes { get; set; }
        public string Summary { get; set; }
    }
}