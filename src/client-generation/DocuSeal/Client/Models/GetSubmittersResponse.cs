using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class GetSubmittersResponse
{
    [JsonPropertyName("data")]
    public List<GetSubmittersResponseDataItem>? Data { get; set; }

    [JsonPropertyName("pagination")]
    public GetSubmittersResponsePagination? Pagination { get; set; }

}
