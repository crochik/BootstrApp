using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CloneTemplateResponseDocumentsItem
{
    /// <summary>
    /// Unique identifier of the document.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    /// <summary>
    /// Unique identifier of the document.
    /// </summary>
    [JsonPropertyName("uuid")]
    public required string Uuid { get; set; }

    /// <summary>
    /// URL of the document.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }

    /// <summary>
    /// Document preview image URL.
    /// </summary>
    [JsonPropertyName("preview_image_url")]
    public required string PreviewImageUrl { get; set; }

    /// <summary>
    /// Document filename.
    /// </summary>
    [JsonPropertyName("filename")]
    public required string Filename { get; set; }

}
