using System;

namespace PI.ProductCatalog.Models
{
    public interface ISLNOnlyProps
    {
        string SKU { get; set; }
        string UniqueColorCode { get; set; }
        string UPCCode { get; set; }
        string ManufacturerSKU { get; set; }
        string ColorName { get; set; }
        string ColorNumber { get; set; }
        string[] ImageUrls { get; set; }
        string ProductSpecification { get; set; }
        DateTime? PromotionalStart { get; set; }
        DateTime? PromotionalEnd { get; set; }
        string[] AssociatedSKUs { get; }
    }
}