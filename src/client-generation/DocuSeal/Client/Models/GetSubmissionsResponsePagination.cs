using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class GetSubmissionsResponsePagination
{
    /// <summary>
    /// Submissions count.
    /// </summary>
    [JsonPropertyName("count")]
    public required int Count { get; set; }

    /// <summary>
    /// The ID of the submission after which the next page starts.
    /// </summary>
    [JsonPropertyName("next")]
    public required int Next { get; set; }

    /// <summary>
    /// The ID of the submission before which the previous page ends.
    /// </summary>
    [JsonPropertyName("prev")]
    public required int Prev { get; set; }

}
