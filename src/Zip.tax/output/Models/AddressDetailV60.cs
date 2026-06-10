using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Geocoded address information for the resolved location
/// </summary>
public class AddressDetailV60
{
    /// <summary>
    /// Normalized full address returned by the geocoder
    /// </summary>
    [JsonPropertyName("normalizedAddress")]
    public required string NormalizedAddress { get; set; }

    /// <summary>
    /// Whether the location is within an incorporated area
    /// </summary>
    [JsonPropertyName("incorporated")]
    public required AddressDetailV60Incorporated Incorporated { get; set; }

    /// <summary>
    /// Geocoded latitude
    /// </summary>
    [JsonPropertyName("geoLat")]
    public required float GeoLat { get; set; }

    /// <summary>
    /// Geocoded longitude
    /// </summary>
    [JsonPropertyName("geoLng")]
    public required float GeoLng { get; set; }

    /// <summary>
    /// Extended address components. Present only when `addressDetailExtended=true` is passed in the request.
    /// </summary>
    [JsonPropertyName("address")]
    public AddressComponentsV60? Address { get; set; }

}
