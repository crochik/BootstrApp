using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Service (labor) taxability for the jurisdiction
/// </summary>
public class ServiceV60
{
    /// <summary>
    /// Adjustment type identifier
    /// </summary>
    [JsonPropertyName("adjustmentType")]
    public required string AdjustmentType { get; set; }

    /// <summary>
    /// Whether services are taxable
    /// </summary>
    [JsonPropertyName("taxable")]
    public required ServiceV60Taxable Taxable { get; set; }

    /// <summary>
    /// Human-readable taxability description
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }

}
