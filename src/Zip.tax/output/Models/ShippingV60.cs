using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Shipping/freight taxability for the jurisdiction
/// </summary>
public class ShippingV60
{
    /// <summary>
    /// Adjustment type identifier
    /// </summary>
    [JsonPropertyName("adjustmentType")]
    public required string AdjustmentType { get; set; }

    /// <summary>
    /// Whether shipping is taxable
    /// </summary>
    [JsonPropertyName("taxable")]
    public required ShippingV60Taxable Taxable { get; set; }

    /// <summary>
    /// Human-readable taxability description
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }

}
