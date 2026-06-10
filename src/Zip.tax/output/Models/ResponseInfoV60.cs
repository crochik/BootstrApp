using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Structured response status information
/// </summary>
public class ResponseInfoV60
{
    /// <summary>
    /// Numeric response code. 100 indicates success; other values indicate specific error conditions.
    /// </summary>
    [JsonPropertyName("code")]
    public required int Code { get; set; }

    /// <summary>
    /// Machine-readable response code name
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable response message
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; set; }

    /// <summary>
    /// Optional extended definition or URL for the response code
    /// </summary>
    [JsonPropertyName("definition")]
    public string? Definition { get; set; }

}
