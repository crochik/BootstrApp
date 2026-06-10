using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// v6.0 structured tax rate response
/// </summary>
public class V60Response
{
    /// <summary>
    /// Response metadata including API version and response status
    /// </summary>
    [JsonPropertyName("metadata")]
    public required MetadataV60 Metadata { get; set; }

    /// <summary>
    /// Base tax rates broken down by jurisdiction
    /// </summary>
    [JsonPropertyName("baseRates")]
    public required List<BaseRateV60> BaseRates { get; set; }

    /// <summary>
    /// Service (labor) taxability for the jurisdiction
    /// </summary>
    [JsonPropertyName("service")]
    public required ServiceV60 Service { get; set; }

    /// <summary>
    /// Shipping/freight taxability for the jurisdiction
    /// </summary>
    [JsonPropertyName("shipping")]
    public required ShippingV60 Shipping { get; set; }

    /// <summary>
    /// Sourcing rules indicating origin-based or destination-based taxation
    /// </summary>
    [JsonPropertyName("sourcingRules")]
    public required OriginDestinationV60 SourcingRules { get; set; }

    /// <summary>
    /// Aggregated tax rate summaries (sales tax and use tax totals)
    /// </summary>
    [JsonPropertyName("taxSummaries")]
    public required List<TaxSummaryV60> TaxSummaries { get; set; }

    /// <summary>
    /// Product-specific taxability detail. Present only when `taxabilityCode` is provided in the request.
    /// </summary>
    [JsonPropertyName("productDetail")]
    public ProductDetailV60? ProductDetail { get; set; }

    /// <summary>
    /// Geocoded address information for the resolved location
    /// </summary>
    [JsonPropertyName("addressDetail")]
    public required AddressDetailV60 AddressDetail { get; set; }

}
