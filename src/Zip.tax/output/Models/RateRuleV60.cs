using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Individual rate rule for a taxability code within a jurisdiction
/// </summary>
public class RateRuleV60
{
    /// <summary>
    /// Jurisdiction tax code
    /// </summary>
    [JsonPropertyName("jurTaxCode")]
    public string? JurTaxCode { get; set; }

    /// <summary>
    /// Effective date as Unix timestamp
    /// </summary>
    [JsonPropertyName("effectiveDt")]
    public long? EffectiveDt { get; set; }

    /// <summary>
    /// Expiration date as Unix timestamp
    /// </summary>
    [JsonPropertyName("expiresDt")]
    public long? ExpiresDt { get; set; }

    /// <summary>
    /// Effective tax rate for this rule
    /// </summary>
    [JsonPropertyName("effectiveTaxRate")]
    public float? EffectiveTaxRate { get; set; }

    /// <summary>
    /// Percentage of the item that is taxable
    /// </summary>
    [JsonPropertyName("percentTaxable")]
    public float? PercentTaxable { get; set; }

    /// <summary>
    /// Exempt if item total is under this amount
    /// </summary>
    [JsonPropertyName("exemptUnder")]
    public float? ExemptUnder { get; set; }

    /// <summary>
    /// Exempt if item total is over this amount
    /// </summary>
    [JsonPropertyName("exemptOver")]
    public float? ExemptOver { get; set; }

    /// <summary>
    /// Only the portion of the item over this amount is taxable
    /// </summary>
    [JsonPropertyName("taxablePortionOver")]
    public float? TaxablePortionOver { get; set; }

    /// <summary>
    /// Whether this rule applies under destination-based sourcing
    /// </summary>
    [JsonPropertyName("isDestinationTaxType")]
    public bool? IsDestinationTaxType { get; set; }

    /// <summary>
    /// Whether this rule applies to food and drug items
    /// </summary>
    [JsonPropertyName("isFoodDrug")]
    public bool? IsFoodDrug { get; set; }

}
