using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class AddDocumentToTemplateRequest
{
    /// <summary>
    /// The list of documents to add or replace in the template.
    /// </summary>
    [JsonPropertyName("documents")]
    public List<AddDocumentToTemplateRequestDocumentsItem>? Documents { get; set; }

    /// <summary>
    /// Set to `true` to merge all existing and new documents into a single PDF document in the template.
    /// </summary>
    [JsonPropertyName("merge")]
    public bool? Merge { get; set; }

}
