using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class UpdateSubmitterRequestFieldsItem
{
    /// <summary>
    /// Document template field name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Default value of the field. Use base64 encoded file or a public URL to the image file to set default signature or image fields.
    /// </summary>
    [JsonPropertyName("default_value")]
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Set `true` to make it impossible for the submitter to edit predefined field value.
    /// </summary>
    [JsonPropertyName("readonly")]
    public bool? Readonly { get; set; }

    /// <summary>
    /// Set `true` to make the field required.
    /// </summary>
    [JsonPropertyName("required")]
    public bool? Required { get; set; }

    [JsonPropertyName("validation")]
    public UpdateSubmitterRequestFieldsItemValidation? Validation { get; set; }

    [JsonPropertyName("preferences")]
    public UpdateSubmitterRequestFieldsItemPreferences? Preferences { get; set; }

}
