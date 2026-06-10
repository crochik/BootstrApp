using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateTemplateFromDocxResponseSubmittersItem
{
    /// <summary>
    /// Submitter name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Unique identifier of the submitter.
    /// </summary>
    [JsonPropertyName("uuid")]
    public required string Uuid { get; set; }

}
