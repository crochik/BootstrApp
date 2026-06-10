using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateTemplateFromPdfRequestDocumentsItem
{
    /// <summary>
    /// Name of the document.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Base64-encoded content of the PDF file or downloadable file URL.
    /// </summary>
    [JsonPropertyName("file")]
    public required string File { get; set; }

    /// <summary>
    /// Fields are optional if you use {{...}} text tags to define fields in the document.
    /// </summary>
    [JsonPropertyName("fields")]
    public List<CreateTemplateFromPdfRequestDocumentsItemFieldsItem>? Fields { get; set; }

}
