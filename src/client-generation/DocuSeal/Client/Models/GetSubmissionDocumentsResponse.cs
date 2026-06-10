using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class GetSubmissionDocumentsResponse
{
    /// <summary>
    /// Submission unique ID number.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    [JsonPropertyName("documents")]
    public required List<GetSubmissionDocumentsResponseDocumentsItem> Documents { get; set; }

}
