using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Individual address components parsed from the geocoded result
/// </summary>
public class AddressComponentsV60
{
    /// <summary>
    /// ISO country code
    /// </summary>
    [JsonPropertyName("countryCode")]
    public required string CountryCode { get; set; }

    /// <summary>
    /// Full country name
    /// </summary>
    [JsonPropertyName("countryName")]
    public required string CountryName { get; set; }

    /// <summary>
    /// Two-letter state abbreviation
    /// </summary>
    [JsonPropertyName("stateCode")]
    public required string StateCode { get; set; }

    /// <summary>
    /// Full state name
    /// </summary>
    [JsonPropertyName("state")]
    public required string State { get; set; }

    /// <summary>
    /// County name
    /// </summary>
    [JsonPropertyName("county")]
    public required string County { get; set; }

    /// <summary>
    /// City name
    /// </summary>
    [JsonPropertyName("city")]
    public required string City { get; set; }

    /// <summary>
    /// Street name
    /// </summary>
    [JsonPropertyName("street")]
    public required string Street { get; set; }

    /// <summary>
    /// Postal code
    /// </summary>
    [JsonPropertyName("postalCode")]
    public required string PostalCode { get; set; }

    /// <summary>
    /// House or building number
    /// </summary>
    [JsonPropertyName("houseNumber")]
    public required string HouseNumber { get; set; }

}
