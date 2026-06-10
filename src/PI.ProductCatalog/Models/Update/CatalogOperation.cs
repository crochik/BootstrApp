using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace PI.ProductCatalog.Models
{
    public enum CatalogSyncOperation
    {
        Unspecified,
        New,
        Update,
        Delete,
        Unchanged,
    }

    [BsonCollection("fcb2b.CatalogUpdateOperation")]
    [BsonKnownTypes(typeof(CatalogStyleOperation), typeof(CatalogItemOperation))]
    [BsonDiscriminator(Required = true)]
    public class CatalogOperation : FlowObjectModel
    {
        public Guid CatalogUpdateId { get; set; }
        public List<ItemCost> Costs { get; set; }
        public List<ItemCost> OverriddenCosts { get; set; } // overriden/ignored
        public CatalogSyncOperation Operation { get; set; }
    }
}