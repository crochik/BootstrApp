using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Base template details.
/// </summary>
public class GetSubmitterResponseTemplate
{
    /// <summary>
    /// The template's unique identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public required double Id { get; set; }

    /// <summary>
    /// The template's name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public required DateTimeOffset UpdatedAt { get; set; }

}
