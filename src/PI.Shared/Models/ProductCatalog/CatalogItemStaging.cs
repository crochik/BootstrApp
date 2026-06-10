using System;
using Crochik.Mongo;

namespace PI.ProductCatalog.Models
{
    [BsonCollection("fcb2b.Item.staging")]
    public class CatalogItemStaging : CatalogItem
    {
        public Guid ParentId { get; set; }
    }
}