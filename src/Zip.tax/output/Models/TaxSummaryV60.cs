using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Aggregated tax rate summary (e.g. total sales tax, total use tax)
/// </summary>
public class TaxSummaryV60
{
    /// <summary>
    /// Combined tax rate for this summary
    /// </summary>
    [JsonPropertyName("rate")]
    public required float Rate { get; set; }

    /// <summary>
    /// Tax type category
    /// </summary>
    [JsonPropertyName("taxType")]
    public required TaxSummaryV60TaxType TaxType { get; set; }

    /// <summary>
    /// Human-readable summary label
    /// </summary>
    [JsonPropertyName("summaryName")]
    public required string SummaryName { get; set; }

    /// <summary>
    /// Breakdown of individual rates contributing to this summary
    /// </summary>
    [JsonPropertyName("displayRates")]
    public required List<DisplayRate> DisplayRates { get; set; }

}
