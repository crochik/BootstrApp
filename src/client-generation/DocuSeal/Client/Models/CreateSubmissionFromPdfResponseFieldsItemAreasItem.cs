using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionFromPdfResponseFieldsItemAreasItem
{
    /// <summary>
    /// X coordinate of the area where the field is located in the document.
    /// </summary>
    [JsonPropertyName("x")]
    public required double X { get; set; }

    /// <summary>
    /// Y coordinate of the area where the field is located in the document.
    /// </summary>
    [JsonPropertyName("y")]
    public required double Y { get; set; }

    /// <summary>
    /// Width of the area where the field is located in the document.
    /// </summary>
    [JsonPropertyName("w")]
    public required double W { get; set; }

    /// <summary>
    /// Height of the area where the field is located in the document.
    /// </summary>
    [JsonPropertyName("h")]
    public required double H { get; set; }

    /// <summary>
    /// Unique identifier of the attached document where the field is located.
    /// </summary>
    [JsonPropertyName("attachment_uuid")]
    public required string AttachmentUuid { get; set; }

    /// <summary>
    /// Page number of the attached document where the field is located.
    /// </summary>
    [JsonPropertyName("page")]
    public required int Page { get; set; }

}
