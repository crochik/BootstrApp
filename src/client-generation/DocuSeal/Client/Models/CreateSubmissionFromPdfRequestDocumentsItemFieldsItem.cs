using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionFromPdfRequestDocumentsItemFieldsItem
{
    /// <summary>
    /// Name of the field.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Type of the field (e.g., text, signature, date, initials).
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Role name of the signer.
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// Indicates if the field is required.
    /// </summary>
    [JsonPropertyName("required")]
    public bool? Required { get; set; }

    /// <summary>
    /// Field title displayed to the user instead of the name, shown on the signing form. Supports Markdown.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Field description displayed on the signing form. Supports Markdown.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("areas")]
    public List<CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem>? Areas { get; set; }

    /// <summary>
    /// An array of option values for 'select' field type.
    /// </summary>
    [JsonPropertyName("options")]
    public List<string>? Options { get; set; }

}
