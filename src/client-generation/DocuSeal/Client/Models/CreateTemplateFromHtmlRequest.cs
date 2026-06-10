using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateTemplateFromHtmlRequest
{
    /// <summary>
    /// HTML template with field tags.
    /// </summary>
    [JsonPropertyName("html")]
    public required string Html { get; set; }

    /// <summary>
    /// HTML template of the header to be displayed on every page.
    /// </summary>
    [JsonPropertyName("html_header")]
    public string? HtmlHeader { get; set; }

    /// <summary>
    /// HTML template of the footer to be displayed on every page.
    /// </summary>
    [JsonPropertyName("html_footer")]
    public string? HtmlFooter { get; set; }

    /// <summary>
    /// Template name. Random uuid will be assigned when not specified.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Page size. Letter 8.5 x 11 will be assigned when not specified.
    /// </summary>
    [JsonPropertyName("size")]
    public string? Size { get; set; }

    /// <summary>
    /// Your application-specific unique string key to identify this template within your app. Existing template with specified `external_id` will be updated with a new HTML.
    /// </summary>
    [JsonPropertyName("external_id")]
    public string? ExternalId { get; set; }

    /// <summary>
    /// The folder's name to which the template should be created.
    /// </summary>
    [JsonPropertyName("folder_name")]
    public string? FolderName { get; set; }

    /// <summary>
    /// set to `true` to make the template available via a shared link. This will allow anyone with the link to create a submission from this template.
    /// </summary>
    [JsonPropertyName("shared_link")]
    public bool? SharedLink { get; set; }

    /// <summary>
    /// The list of documents built from HTML. Can be used to create a template with multiple documents. Leave `documents` param empty when using a top-level `html` param for a template with a single document.
    /// </summary>
    [JsonPropertyName("documents")]
    public List<CreateTemplateFromHtmlRequestDocumentsItem>? Documents { get; set; }

}
