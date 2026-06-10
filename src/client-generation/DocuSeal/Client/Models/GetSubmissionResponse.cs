using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class GetSubmissionResponse
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
    /// Unique slug of the submission.
    /// </summary>
    [JsonPropertyName("slug")]
    public required string Slug { get; set; }

    /// <summary>
    /// The source of the submission.
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; set; }

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
    public required string CombinedDocumentUrl { get; set; }

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
    public required string ArchivedAt { get; set; }

    /// <summary>
    /// The list of submitters.
    /// </summary>
    [JsonPropertyName("submitters")]
    public required List<GetSubmissionResponseSubmittersItem> Submitters { get; set; }

    [JsonPropertyName("template")]
    public GetSubmissionResponseTemplate? Template { get; set; }

    [JsonPropertyName("created_by_user")]
    public required GetSubmissionResponseCreatedByUser CreatedByUser { get; set; }

    [JsonPropertyName("submission_events")]
    public required List<GetSubmissionResponseSubmissionEventsItem> SubmissionEvents { get; set; }

    /// <summary>
    /// An array of completed or signed documents of the submission.
    /// </summary>
    [JsonPropertyName("documents")]
    public required List<GetSubmissionResponseDocumentsItem> Documents { get; set; }

    /// <summary>
    /// The status of the submission.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    /// <summary>
    /// Object with custom metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public required object Metadata { get; set; }

    /// <summary>
    /// The date and time when the submission was fully completed.
    /// </summary>
    [JsonPropertyName("completed_at")]
    public required string CompletedAt { get; set; }

}
