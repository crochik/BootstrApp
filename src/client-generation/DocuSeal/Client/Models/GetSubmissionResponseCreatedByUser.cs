using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class GetSubmissionResponseCreatedByUser
{
    /// <summary>
    /// Unique identifier of the user who created the submission.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    /// <summary>
    /// The first name of the user who created the submission.
    /// </summary>
    [JsonPropertyName("first_name")]
    public required string FirstName { get; set; }

    /// <summary>
    /// The last name of the user who created the submission.
    /// </summary>
    [JsonPropertyName("last_name")]
    public required string LastName { get; set; }

    /// <summary>
    /// The email address of the user who created the submission.
    /// </summary>
    [JsonPropertyName("email")]
    public required string Email { get; set; }

}
