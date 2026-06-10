using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateTemplateFromPdfResponseSchemaItem
{
    /// <summary>
    /// Unique indentifier of attached document to the template.
    /// </summary>
    [JsonPropertyName("attachment_uuid")]
    public required string AttachmentUuid { get; set; }

    /// <summary>
    /// Name of the attached document to the template.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

}
