using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Salesforce.Models;

public class AbstractSfLineItem : SfObject
{
    [BsonElement("Name")] public string Name { get; set; }
    [BsonElement("Name__c")] public string Name2 { get; set; }

    [BsonElement("AdjQuantity__c")] public decimal? AdjustedQuantity { get; set; }
    // [BsonElement("Cost1__c")] public decimal? Cost1 { get; set; }
    // [BsonElement("Cost2__c")] public decimal? Cost2 { get; set; }
    [BsonElement("Description__c")] public string Description { get; set; }
    // [BsonElement("ManualType__c")] public decimal? ManualType { get; set; }
    // [BsonElement("Margin__c")] public decimal? Margin { get; set; }
    // [BsonElement("Price1__c")] public decimal? Price1 { get; set; }
    // [BsonElement("Price2__c")] public decimal? Price2 { get; set; }
    // [BsonElement("PriceType__c")] public string PriceType { get; set; }
    [BsonElement("Product__c")] public string Product { get; set; }
    [BsonElement("Quantity__c")] public decimal? Quantity { get; set; }
    
    
    /// <summary>
    /// since it is calculated, it may be stored as a float e.g. 7.70421960107551E-4
    /// </summary>
    // [BsonElement("RealWaste__c")] public float? RealWaste { get; set; }
    
    [BsonElement("TaxFactor__c")] public decimal? TaxFactor { get; set; }
    
    [BsonElement("TaxGroup__c")] public string TaxGroup { get; set; }
    
    [BsonElement("TaxPrice__c")] public decimal? TaxPrice { get; set; }
    [BsonElement("TotalBeforeTax__c")] public decimal? TotalBeforeTax { get; set; }
    [BsonElement("TotalCost__c")] public decimal? TotalCost { get; set; }
    [BsonElement("TotalPrice__c")] public decimal? TotalPrice { get; set; }
    
    // [BsonElement("UnitCost__c")] public decimal? UnitCost { get; set; }
    
    [BsonElement("UnitPrice__c")] public decimal? UnitPrice { get; set; }
    
    // [BsonElement("WasteFactor__c")] public decimal? WasteFactor { get; set; }
    
    [BsonElement("Index__c")] public int? Index { get; set; }
    // public int? Index => _index != null && decimal.TryParse(_index, out var i) ? (int)i : null;
}