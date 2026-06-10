using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateTemplateFromDocxRequestDocumentsItemFieldsItemAreasItem
{
    /// <summary>
    /// X-coordinate of the field area.
    /// </summary>
    [JsonPropertyName("x")]
    public double? X { get; set; }

    /// <summary>
    /// Y-coordinate of the field area.
    /// </summary>
    [JsonPropertyName("y")]
    public double? Y { get; set; }

    /// <summary>
    /// Width of the field area.
    /// </summary>
    [JsonPropertyName("w")]
    public double? W { get; set; }

    /// <summary>
    /// Height of the field area.
    /// </summary>
    [JsonPropertyName("h")]
    public double? H { get; set; }

    /// <summary>
    /// Page number of the field area. Starts from 1.
    /// </summary>
    [JsonPropertyName("page")]
    public int? Page { get; set; }

    /// <summary>
    /// Option string value for 'radio' and 'multiple' select field types.
    /// </summary>
    [JsonPropertyName("option")]
    public string? Option { get; set; }

}
