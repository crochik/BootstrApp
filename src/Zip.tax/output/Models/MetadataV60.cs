using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Response metadata including API version and response status
/// </summary>
public class MetadataV60
{
    /// <summary>
    /// API version identifier
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; set; }

    /// <summary>
    /// Structured response status information
    /// </summary>
    [JsonPropertyName("response")]
    public required ResponseInfoV60 Response { get; set; }

}
