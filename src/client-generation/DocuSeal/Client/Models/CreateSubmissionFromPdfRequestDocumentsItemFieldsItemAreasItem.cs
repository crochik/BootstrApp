using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem
{
    /// <summary>
    /// X-coordinate of the field area.
    /// </summary>
    [JsonPropertyName("x")]
    public required double X { get; set; }

    /// <summary>
    /// Y-coordinate of the field area.
    /// </summary>
    [JsonPropertyName("y")]
    public required double Y { get; set; }

    /// <summary>
    /// Width of the field area.
    /// </summary>
    [JsonPropertyName("w")]
    public required double W { get; set; }

    /// <summary>
    /// Height of the field area.
    /// </summary>
    [JsonPropertyName("h")]
    public required double H { get; set; }

    /// <summary>
    /// Page number of the field area. Starts from 1.
    /// </summary>
    [JsonPropertyName("page")]
    public required int Page { get; set; }

    /// <summary>
    /// Option string value for 'radio' and 'multiple' select field types.
    /// </summary>
    [JsonPropertyName("option")]
    public string? Option { get; set; }

}
