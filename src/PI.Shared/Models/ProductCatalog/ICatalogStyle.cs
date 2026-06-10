using System;

namespace PI.ProductCatalog.Models;

public interface ICatalogStyle
{
    string Manufacturer { get; set; }
    string StyleNumber { get; set; }
    string PricingGroup { get; set; }
    string BuyingGroup { get; set; }
    string Contract { get; set; }
    string SellingCompany { get; set; }
    string Gauge { get; set; }
    DateTime? EffectiveDate { get; set; }
    DateTime? PendingDate { get; set; }
    DateTime? DroppedDate { get; set; }
    MaterialClassification Material { get; set; }
    string ProductType { get; set; }
    string Composition { get; set; }
    string CollectionName { get; set; }
    string PrimaryComponent { get; set; }
    Warranty Warranty { get; set; }
    BuilderProgram BuilderProgram { get; set; }
    Models.UOMRate[] Packaging { get; set; }
    string UniqueStyleCode { get; set; }
    string StyleName { get; set; }
    string ManufacturerStyleNumber { get; set; }
    string ManufacturerStyleName { get; set; }
    string Backing { get; set; }
    string SizeCode { get; set; }
    string AbbreviatedProductName { get; set; }
    Measurement PatternRepeat { get; set; }
    Measurement PatternDrop { get; set; }
    Measurement PatternLength { get; set; }
    Measurement PatternWidth { get; set; }
    Measurement NominalLength { get; set; }
    Measurement ActualLength { get; set; }
    Measurement NominalWidth { get; set; }
    Measurement ActualWidth { get; set; }
    Measurement Height { get; set; }
    UOMRate ShippingWeight { get; set; }
    Measurement FaceWeight { get; set; }
    Measurement SellingUnit { get; set; }
    string[] AssociatedStyleNumbers { get; set; }
    BaseUnit BaseUnit { get; set; }
}