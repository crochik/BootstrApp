using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class GetSubmittersResponseDataItem
{
    /// <summary>
    /// Submitter unique ID number.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    /// <summary>
    /// Submission unique ID number.
    /// </summary>
    [JsonPropertyName("submission_id")]
    public required int SubmissionId { get; set; }

    /// <summary>
    /// Submitter UUID.
    /// </summary>
    [JsonPropertyName("uuid")]
    public required string Uuid { get; set; }

    /// <summary>
    /// The email address of the submitter.
    /// </summary>
    [JsonPropertyName("email")]
    public required string Email { get; set; }

    /// <summary>
    /// Unique slug of the submitter form.
    /// </summary>
    [JsonPropertyName("slug")]
    public required string Slug { get; set; }

    /// <summary>
    /// The date and time when the signing request was sent to the submitter.
    /// </summary>
    [JsonPropertyName("sent_at")]
    public required string SentAt { get; set; }

    /// <summary>
    /// The date and time when the submitter opened the signing form.
    /// </summary>
    [JsonPropertyName("opened_at")]
    public required string OpenedAt { get; set; }

    /// <summary>
    /// The date and time when the submitter completed the signing form.
    /// </summary>
    [JsonPropertyName("completed_at")]
    public required string CompletedAt { get; set; }

    /// <summary>
    /// The date and time when the submitter declined the signing form.
    /// </summary>
    [JsonPropertyName("declined_at")]
    public required string DeclinedAt { get; set; }

    /// <summary>
    /// The date and time when the submitter was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; set; }

    /// <summary>
    /// The date and time when the submitter was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public required string UpdatedAt { get; set; }

    /// <summary>
    /// Submitter name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Submitter phone number.
    /// </summary>
    [JsonPropertyName("phone")]
    public required string Phone { get; set; }

    /// <summary>
    /// Submitter's submission status.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    /// <summary>
    /// The unique applications-specific identifier
    /// </summary>
    [JsonPropertyName("external_id")]
    public required string ExternalId { get; set; }

    /// <summary>
    /// Submitter preferences.
    /// </summary>
    [JsonPropertyName("preferences")]
    public required object Preferences { get; set; }

    /// <summary>
    /// Metadata object with additional submitter information.
    /// </summary>
    [JsonPropertyName("metadata")]
    public required object Metadata { get; set; }

    [JsonPropertyName("submission_events")]
    public required List<GetSubmittersResponseDataItemSubmissionEventsItem> SubmissionEvents { get; set; }

    /// <summary>
    /// An array of pre-filled values for the submission.
    /// </summary>
    [JsonPropertyName("values")]
    public required List<GetSubmittersResponseDataItemValuesItem> Values { get; set; }

    /// <summary>
    /// An array of completed or signed documents by the submitter.
    /// </summary>
    [JsonPropertyName("documents")]
    public required List<GetSubmittersResponseDataItemDocumentsItem> Documents { get; set; }

    /// <summary>
    /// The role of the submitter in the signing process.
    /// </summary>
    [JsonPropertyName("role")]
    public required string Role { get; set; }

}
