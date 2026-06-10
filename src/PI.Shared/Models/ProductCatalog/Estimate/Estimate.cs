using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;
using PI.Shared.Models.Interfaces;

namespace PI.ProductCatalog.Models;

public class DiscountRate
{
    /// <summary>
    /// applies only to items in this category (if specified)
    /// </summary>
    public TaxCategory Category { get; set; }
    
    /// <summary>
    /// Percentage (100 - [value])/100
    /// </summary>
    public decimal Rate { get; set; }
    
    /// <summary>
    /// Calculated amount
    /// </summary>
    public decimal? Amount { get; set; }
}

[BsonCollection("fcb2b.Discount")]
public class Discount : FlowObjectModel
{
    public DiscountRate[] DiscountsRates { get; set; }
    
    /// <summary>
    /// (calculated) $ change in price 
    /// </summary>
    public decimal? PriceDiscount { get; set; }
    
    /// <summary>
    /// (calculated) $ change in discount 
    /// </summary>
    public decimal? TaxDiscount { get; set; }
}

public enum SectionPosition
{
    Before, 
    After
}

public class EstimateSection
{
    public string Name { get; set; }
    public string ContentType { get; set; }
    public string Content { get; set; }
    
    // TODO:...
    public SectionPosition Position { get; set; }
}

public class EstimateAttachment
{
    public string Tag { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Guid RemoteFileId { get; set; }
}

[BsonCollection("fcb2b.Estimate")]
public class Estimate : FlowObjectModel, ITaggable, IWithRelatedObjects, ITaxable
{
    public const string ObjectTypeFullName = "otg.Estimate"; // TODO: NAMESPACE !?!??!?!?!?
    public override string ObjectType => ObjectTypeFullName;

    /// <summary>
    /// (AI) Summary
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// User Friendly unique identifier
    /// (unique counter per Org?)
    /// </summary>
    public string EstimateNumber { get; set; }

    /// <summary>
    /// Version of the proposal
    /// </summary>
    public int Version { get; set; } = 1;

    public Guid LeadId { get; set; }
    public Guid ProjectId { get; set; }
    
    public string ProjectExternalId { get; set; }

    /// <summary>
    /// Room Selection Ids, if any, used to generate this estimate 
    /// </summary>
    public Guid[] RoomSelectionIds { get; set; }

    public Guid CreatedBy { get; set; }

    public LineItem[] LineItems { get; set; }

    public decimal? TotalCost { get; set; }
    
    /// <summary>
    /// Before discount
    /// </summary>
    public decimal? TotalPrice { get; set; }
    
    /// <summary>
    /// Before discount
    /// </summary>
    public decimal? TotalTax { get; set; }

    public decimal? BlendedMargin { get; set; }
    
    /// <summary>
    /// Discount on the price 
    /// </summary>
    public decimal? DiscountPrice { get; set; }
    
    /// <summary>
    /// Discount on tax
    /// </summary>
    public decimal? DiscountTax { get; set; }

    /// <summary>
    /// Total (price - discount ) + tax
    /// </summary>
    [BsonElement]
    public decimal GrandTotal => decimal.Round((TotalPrice ?? 0) - (DiscountPrice ?? 0) + GrandTax,2);

    /// <summary>
    /// TotalTax - DiscountTax 
    /// </summary>
    [BsonElement]
    public decimal GrandTax => decimal.Round((TotalTax ?? 0) - (DiscountTax ?? 0),2);

    public bool HasWarnings { get; set; }

    /// <summary>
    /// Tax Rates used 
    /// </summary>
    public TaxRates TaxRates { get; set; }

    /// <summary>
    /// Tax Liability calculated
    /// </summary>
    public TaxLiability[] TaxLiabilities { get; set; }

    /// <summary>
    /// Discount(s) applied
    /// </summary>
    public Discount[] Discounts { get; set; }
    
    /// <summary>
    /// Tags (used as flags?)
    /// </summary>
    public string[] Tags { get; set; }
    
    /// <summary>
    /// arbitrary sections
    /// </summary>
    public EstimateSection[] Sections { get; set; }

    public Dictionary<string, object> RelatedObjects { get; set; }
    
    public Dictionary<string, EstimateAttachment> Attachments { get; set; }
    public bool IsNonTaxable { get; set; }
}

