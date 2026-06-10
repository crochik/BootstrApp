using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Sourcing rules indicating origin-based or destination-based taxation
/// </summary>
public class OriginDestinationV60
{
    /// <summary>
    /// Adjustment type identifier
    /// </summary>
    [JsonPropertyName("adjustmentType")]
    public required string AdjustmentType { get; set; }

    /// <summary>
    /// Human-readable sourcing rule description
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    /// <summary>
    /// O = Origin-based, D = Destination-based
    /// </summary>
    [JsonPropertyName("value")]
    public required OriginDestinationV60Value Value { get; set; }

}
