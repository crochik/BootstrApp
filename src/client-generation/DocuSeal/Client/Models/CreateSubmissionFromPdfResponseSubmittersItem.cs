using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionFromPdfResponseSubmittersItem
{
    /// <summary>
    /// Submission unique ID number.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

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
    /// Unique key to be used in the form signing link and embedded form.
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
    /// The date and time when the submitter declined to complete the signing form.
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
    /// Your application-specific unique string key to identify this submitter within your app.
    /// </summary>
    [JsonPropertyName("external_id")]
    public string? ExternalId { get; set; }

    /// <summary>
    /// The status of signing request for the submitter.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    /// <summary>
    /// An array of pre-filled values for the submitter.
    /// </summary>
    [JsonPropertyName("values")]
    public List<CreateSubmissionFromPdfResponseSubmittersItemValuesItem>? Values { get; set; }

    /// <summary>
    /// The role of the submitter.
    /// </summary>
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    /// <summary>
    /// Metadata object with additional submitter information.
    /// </summary>
    [JsonPropertyName("metadata")]
    public required object Metadata { get; set; }

    /// <summary>
    /// Object with submitter preferences.
    /// </summary>
    [JsonPropertyName("preferences")]
    public required object Preferences { get; set; }

    /// <summary>
    /// The `src` URL value to embed the signing form or sign via a link.
    /// </summary>
    [JsonPropertyName("embed_src")]
    public string? EmbedSrc { get; set; }

}
