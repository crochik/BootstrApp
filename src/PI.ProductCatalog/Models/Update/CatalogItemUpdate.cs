using System;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.ProductCatalog.Models;

[BsonDiscriminator("Item")]
public class CatalogItemUpdate : CatalogStyleUpdate, ICatalogItem
{
    /// <summary>
    /// Calculate item name
    /// </summary>
    public string Name { get; set; }

    public string UPCCode { get; set; }
    public string SKU { get; set; }
    public string ManufacturerSKU { get; set; }
    public string ColorName { get; set; }
    public string ColorNumber { get; set; }
    public string[] ImageUrls { get; set; }
    public string ProductSpecification { get; set; }
    public DateTime? PromotionalStart { get; set; }
    public DateTime? PromotionalEnd { get; set; }
    public string UniqueColorCode { get; set; }
    public string[] AssociatedSKUs { get; set; }

    public string ExternalId { get; set; }
}

public static class CatalogItemExtensions
{
    public static void UpdateName(this CatalogItem item, Edi832.ICatalogFormat sender)
    {
        if (item == null) return;

        if (sender.UseStyleAndColorName || string.IsNullOrWhiteSpace(item.AbbreviatedProductName))
        {
            var styleName = item.GetStyleName()?.Trim();
            var colorName = item.GetColorName()?.Trim();
            if (!string.IsNullOrWhiteSpace(styleName) && !string.IsNullOrWhiteSpace(colorName))
            {
                item.Name = $"{styleName} {colorName}";
                return;
            }
        }

        item.Name = item.AbbreviatedProductName;
    }
}