using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class GetSubmissionResponseDocumentsItem
{
    /// <summary>
    /// Document name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Document URL.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }

}
