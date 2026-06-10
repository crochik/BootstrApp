using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class GetTemplatesResponse
{
    /// <summary>
    /// List of templates.
    /// </summary>
    [JsonPropertyName("data")]
    public required List<GetTemplatesResponseDataItem> Data { get; set; }

    [JsonPropertyName("pagination")]
    public required GetTemplatesResponsePagination Pagination { get; set; }

}
