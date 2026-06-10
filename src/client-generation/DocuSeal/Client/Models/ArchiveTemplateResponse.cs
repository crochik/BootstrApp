using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class ArchiveTemplateResponse
{
    /// <summary>
    /// Template unique ID number.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    /// <summary>
    /// Date and time when the template was archived.
    /// </summary>
    [JsonPropertyName("archived_at")]
    public required string ArchivedAt { get; set; }

}
