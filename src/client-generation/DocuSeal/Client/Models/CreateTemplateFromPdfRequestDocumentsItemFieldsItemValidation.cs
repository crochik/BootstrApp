using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateTemplateFromPdfRequestDocumentsItemFieldsItemValidation
{
    /// <summary>
    /// HTML field validation pattern string based on https://developer.mozilla.org/en-US/docs/Web/HTML/Attributes/pattern specification.
    /// </summary>
    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    /// <summary>
    /// A custom error message to display on validation failure.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Minimum allowed number value or date depending on field type.
    /// </summary>
    [JsonPropertyName("min")]
    public object? Min { get; set; }

    /// <summary>
    /// Maximum allowed number value or date depending on field type.
    /// </summary>
    [JsonPropertyName("max")]
    public object? Max { get; set; }

    /// <summary>
    /// Increment step for number field. Pass 1 to accept only integers, or 0.01 to accept decimal currency.
    /// </summary>
    [JsonPropertyName("step")]
    public double? Step { get; set; }

}
