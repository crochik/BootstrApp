using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class GetSubmissionsResponse
{
    [JsonPropertyName("data")]
    public required List<GetSubmissionsResponseDataItem> Data { get; set; }

    [JsonPropertyName("pagination")]
    public required GetSubmissionsResponsePagination Pagination { get; set; }

}
