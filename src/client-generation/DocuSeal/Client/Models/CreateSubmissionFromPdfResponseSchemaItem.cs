using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionFromPdfResponseSchemaItem
{
    /// <summary>
    /// The attachment UUID.
    /// </summary>
    [JsonPropertyName("attachment_uuid")]
    public string? AttachmentUuid { get; set; }

    /// <summary>
    /// The attachment name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

}
