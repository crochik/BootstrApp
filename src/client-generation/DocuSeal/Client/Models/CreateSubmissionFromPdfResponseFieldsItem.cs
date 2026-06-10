using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionFromPdfResponseFieldsItem
{
    /// <summary>
    /// Unique identifier of the field.
    /// </summary>
    [JsonPropertyName("uuid")]
    public required string Uuid { get; set; }

    /// <summary>
    /// Unique identifier of the submitter that filled the field.
    /// </summary>
    [JsonPropertyName("submitter_uuid")]
    public required string SubmitterUuid { get; set; }

    /// <summary>
    /// Field name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Type of the field (e.g., text, signature, date, initials).
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// Indicates if the field is required.
    /// </summary>
    [JsonPropertyName("required")]
    public required bool Required { get; set; }

    [JsonPropertyName("preferences")]
    public CreateSubmissionFromPdfResponseFieldsItemPreferences? Preferences { get; set; }

    /// <summary>
    /// List of areas where the field is located in the document.
    /// </summary>
    [JsonPropertyName("areas")]
    public required List<CreateSubmissionFromPdfResponseFieldsItemAreasItem> Areas { get; set; }

}
