using System;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.ProductCatalog.Models
{
    [BsonDiscriminator("Style")]
    public class CatalogStyleUpdate : ICatalogStyle
    {
        public string Manufacturer { get; set; }
        public string StyleNumber { get; set; }

        public string PricingGroup { get; set; }
        public string BuyingGroup { get; set; }
        public string Contract { get; set; }
        public string SellingCompany { get; set; }
        public string Gauge { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? PendingDate { get; set; }
        public DateTime? DroppedDate { get; set; }
        public MaterialClassification Material { get; set; }
        public string ProductType { get; set; }
        public string Composition { get; set; }
        public string CollectionName { get; set; }
        public string PrimaryComponent { get; set; }
        public Warranty Warranty { get; set; }
        public BuilderProgram BuilderProgram { get; set; }
        public Models.UOMRate[] Packaging { get; set; }
        public string UniqueStyleCode { get; set; }
        public string StyleName { get; set; }
        public string ManufacturerStyleNumber { get; set; }
        public string ManufacturerStyleName { get; set; }
        public string Backing { get; set; }
        public string SizeCode { get; set; }
        public string AbbreviatedProductName { get; set; }
        public Measurement PatternRepeat { get; set; }
        public Measurement PatternDrop { get; set; }
        public Measurement PatternLength { get; set; }
        public Measurement PatternWidth { get; set; }
        public Measurement NominalLength { get; set; }
        public Measurement ActualLength { get; set; }
        public Measurement NominalWidth { get; set; }
        public Measurement ActualWidth { get; set; }
        public Measurement Height { get; set; }
        public UOMRate ShippingWeight { get; set; }
        public Measurement FaceWeight { get; set; }
        public Measurement SellingUnit { get; set; }

        public string[] AssociatedStyleNumbers { get; set; }

        public BaseUnit BaseUnit { get; set; }
    }
}