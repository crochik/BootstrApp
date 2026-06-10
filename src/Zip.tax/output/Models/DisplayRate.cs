using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Individual rate contributing to a tax summary
/// </summary>
public class DisplayRate
{
    /// <summary>
    /// Jurisdiction label (e.g. "State", "County", "District")
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Tax rate as a decimal
    /// </summary>
    [JsonPropertyName("rate")]
    public required float Rate { get; set; }

}
