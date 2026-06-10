using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class GetSubmissionsResponseDataItem
{
    /// <summary>
    /// Submission unique ID number.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    /// <summary>
    /// Name of the document submission
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The source of the submission.
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; set; }

    /// <summary>
    /// Unique slug of the submission.
    /// </summary>
    [JsonPropertyName("slug")]
    public required string Slug { get; set; }

    /// <summary>
    /// The status of the submission.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    /// <summary>
    /// The order of submitters.
    /// </summary>
    [JsonPropertyName("submitters_order")]
    public required string SubmittersOrder { get; set; }

    /// <summary>
    /// Audit log file URL.
    /// </summary>
    [JsonPropertyName("audit_log_url")]
    public required string AuditLogUrl { get; set; }

    /// <summary>
    /// Combined PDF file URL with documents and Audit Log.
    /// </summary>
    [JsonPropertyName("combined_document_url")]
    public string? CombinedDocumentUrl { get; set; }

    /// <summary>
    /// The date and time when the submission was completed.
    /// </summary>
    [JsonPropertyName("completed_at")]
    public required string CompletedAt { get; set; }

    /// <summary>
    /// The date and time when the submission was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; set; }

    /// <summary>
    /// The date and time when the submission was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public required string UpdatedAt { get; set; }

    /// <summary>
    /// The date and time when the submission was archived.
    /// </summary>
    [JsonPropertyName("archived_at")]
    public string? ArchivedAt { get; set; }

    /// <summary>
    /// The list of submitters.
    /// </summary>
    [JsonPropertyName("submitters")]
    public required List<GetSubmissionsResponseDataItemSubmittersItem> Submitters { get; set; }

    [JsonPropertyName("template")]
    public GetSubmissionsResponseDataItemTemplate? Template { get; set; }

    [JsonPropertyName("created_by_user")]
    public required GetSubmissionsResponseDataItemCreatedByUser CreatedByUser { get; set; }

}
