using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Individual tax rate for a single jurisdiction
/// </summary>
public class BaseRateV60
{
    /// <summary>
    /// Tax rate as a decimal (e.g. 0.06 = 6%)
    /// </summary>
    [JsonPropertyName("rate")]
    public required float Rate { get; set; }

    [JsonPropertyName("jurType")]
    public required string JurType { get; set; }

    [JsonPropertyName("jurName")]
    public required string JurName { get; set; }

    /// <summary>
    /// Human-readable jurisdiction description
    /// </summary>
    [JsonPropertyName("jurDescription")]
    public required string JurDescription { get; set; }

    /// <summary>
    /// Tax code for the jurisdiction. Null when no specific code applies.
    /// </summary>
    [JsonPropertyName("jurTaxCode")]
    public string? JurTaxCode { get; set; }

}
