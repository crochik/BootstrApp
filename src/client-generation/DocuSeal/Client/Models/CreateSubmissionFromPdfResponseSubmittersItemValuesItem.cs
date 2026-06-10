using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionFromPdfResponseSubmittersItemValuesItem
{
    /// <summary>
    /// Document template field name.
    /// </summary>
    [JsonPropertyName("field")]
    public required string Field { get; set; }

    /// <summary>
    /// Pre-filled value of the field.
    /// </summary>
    [JsonPropertyName("value")]
    public required object Value { get; set; }

}
