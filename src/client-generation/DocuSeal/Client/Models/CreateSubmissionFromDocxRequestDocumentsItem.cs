using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionFromDocxRequestDocumentsItem
{
    /// <summary>
    /// Name of the document.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Base64-encoded content of the PDF or DOCX file or downloadable file URL.
    /// </summary>
    [JsonPropertyName("file")]
    public required string File { get; set; }

    /// <summary>
    /// Document position in the submission. If not specified, the document will be added in the order it appears in the documents array.
    /// </summary>
    [JsonPropertyName("position")]
    public int? Position { get; set; }

}
