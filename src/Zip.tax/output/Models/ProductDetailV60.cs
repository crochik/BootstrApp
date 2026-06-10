using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Product-specific taxability information based on the requested taxability code
/// </summary>
public class ProductDetailV60
{
    /// <summary>
    /// Taxability code details and rate rules
    /// </summary>
    [JsonPropertyName("taxabilityCode")]
    public required TaxabilityCodeV60 TaxabilityCode { get; set; }

}
