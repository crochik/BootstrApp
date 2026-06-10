using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionFromDocxResponse
{
    /// <summary>
    /// Submission unique ID number.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    /// <summary>
    /// Submission name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The list of submitters.
    /// </summary>
    [JsonPropertyName("submitters")]
    public required List<CreateSubmissionFromDocxResponseSubmittersItem> Submitters { get; set; }

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
    /// The status of the submission.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    /// <summary>
    /// The one-off submission document files.
    /// </summary>
    [JsonPropertyName("schema")]
    public List<CreateSubmissionFromDocxResponseSchemaItem>? Schema { get; set; }

    /// <summary>
    /// List of fields to be filled in the one-off submission.
    /// </summary>
    [JsonPropertyName("fields")]
    public List<CreateSubmissionFromDocxResponseFieldsItem>? Fields { get; set; }

    /// <summary>
    /// Specify the expiration date and time after which the submission becomes unavailable for signature.
    /// </summary>
    [JsonPropertyName("expire_at")]
    public required string ExpireAt { get; set; }

    /// <summary>
    /// The date and time when the submission was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; set; }

}
