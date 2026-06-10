using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Taxability code details and rate rules
/// </summary>
public class TaxabilityCodeV60
{
    /// <summary>
    /// Taxability code identifier
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// State FIPS code
    /// </summary>
    [JsonPropertyName("stateFIPS")]
    public required string StateFips { get; set; }

    /// <summary>
    /// County FIPS code
    /// </summary>
    [JsonPropertyName("countyFIPS")]
    public required string CountyFips { get; set; }

    /// <summary>
    /// Taxability code title
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    /// <summary>
    /// Taxability code label
    /// </summary>
    [JsonPropertyName("label")]
    public required string Label { get; set; }

    /// <summary>
    /// Rate action code indicating how the rate should be applied
    /// </summary>
    [JsonPropertyName("rateActionCode")]
    public required string RateActionCode { get; set; }

    /// <summary>
    /// Human-readable rate action description
    /// </summary>
    [JsonPropertyName("rateActionMessage")]
    public required string RateActionMessage { get; set; }

    /// <summary>
    /// Jurisdiction-specific rate rules for this taxability code
    /// </summary>
    [JsonPropertyName("rateRules")]
    public required List<RateRuleV60> RateRules { get; set; }

}
