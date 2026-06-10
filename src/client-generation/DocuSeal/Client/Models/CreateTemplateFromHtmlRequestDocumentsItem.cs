using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateTemplateFromHtmlRequestDocumentsItem
{
    /// <summary>
    /// HTML template with field tags.
    /// </summary>
    [JsonPropertyName("html")]
    public required string Html { get; set; }

    /// <summary>
    /// Document name. Random uuid will be assigned when not specified.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

}
