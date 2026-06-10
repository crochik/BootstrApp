using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CloneTemplateResponse
{
    /// <summary>
    /// Unique identifier of the document template.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    /// <summary>
    /// Unique slug of the document template.
    /// </summary>
    [JsonPropertyName("slug")]
    public required string Slug { get; set; }

    /// <summary>
    /// Name of the template.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Template preferences.
    /// </summary>
    [JsonPropertyName("preferences")]
    public required object Preferences { get; set; }

    /// <summary>
    /// List of documents attached to the template.
    /// </summary>
    [JsonPropertyName("schema")]
    public required List<CloneTemplateResponseSchemaItem> Schema { get; set; }

    /// <summary>
    /// List of fields to be filled in the template.
    /// </summary>
    [JsonPropertyName("fields")]
    public required List<CloneTemplateResponseFieldsItem> Fields { get; set; }

    [JsonPropertyName("submitters")]
    public required List<CloneTemplateResponseSubmittersItem> Submitters { get; set; }

    /// <summary>
    /// Unique identifier of the author of the template.
    /// </summary>
    [JsonPropertyName("author_id")]
    public required int AuthorId { get; set; }

    /// <summary>
    /// Date and time when the template was archived.
    /// </summary>
    [JsonPropertyName("archived_at")]
    public required string ArchivedAt { get; set; }

    /// <summary>
    /// Date and time when the template was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; set; }

    /// <summary>
    /// Date and time when the template was updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public required string UpdatedAt { get; set; }

    /// <summary>
    /// Source of the template.
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; set; }

    /// <summary>
    /// Identifier of the template in the external system.
    /// </summary>
    [JsonPropertyName("external_id")]
    public required string ExternalId { get; set; }

    /// <summary>
    /// Unique identifier of the folder where the template is placed.
    /// </summary>
    [JsonPropertyName("folder_id")]
    public required int FolderId { get; set; }

    /// <summary>
    /// Folder name where the template is placed.
    /// </summary>
    [JsonPropertyName("folder_name")]
    public required string FolderName { get; set; }

    /// <summary>
    /// Indicates if the template is accessible by link.
    /// </summary>
    [JsonPropertyName("shared_link")]
    public bool? SharedLink { get; set; }

    [JsonPropertyName("author")]
    public required CloneTemplateResponseAuthor Author { get; set; }

    /// <summary>
    /// List of documents attached to the template.
    /// </summary>
    [JsonPropertyName("documents")]
    public required List<CloneTemplateResponseDocumentsItem> Documents { get; set; }

}
