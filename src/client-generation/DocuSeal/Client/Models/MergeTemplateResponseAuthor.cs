using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class MergeTemplateResponseAuthor
{
    /// <summary>
    /// Unique identifier of the author.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    /// <summary>
    /// First name of the author.
    /// </summary>
    [JsonPropertyName("first_name")]
    public required string FirstName { get; set; }

    /// <summary>
    /// Last name of the author.
    /// </summary>
    [JsonPropertyName("last_name")]
    public required string LastName { get; set; }

    /// <summary>
    /// Author email.
    /// </summary>
    [JsonPropertyName("email")]
    public required string Email { get; set; }

}
