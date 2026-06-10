using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class UpdateTemplateResponse
{
    /// <summary>
    /// Template unique ID number.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    /// <summary>
    /// Date and time when the template was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public required string UpdatedAt { get; set; }

}
