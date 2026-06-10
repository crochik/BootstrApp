using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionFromHtmlRequestDocumentsItem
{
    /// <summary>
    /// Document name. Random uuid will be assigned when not specified.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// HTML document content with field tags.
    /// </summary>
    [JsonPropertyName("html")]
    public required string Html { get; set; }

    /// <summary>
    /// HTML document content of the header to be displayed on every page.
    /// </summary>
    [JsonPropertyName("html_header")]
    public string? HtmlHeader { get; set; }

    /// <summary>
    /// HTML document content of the footer to be displayed on every page.
    /// </summary>
    [JsonPropertyName("html_footer")]
    public string? HtmlFooter { get; set; }

    /// <summary>
    /// Page size. Letter 8.5 x 11 will be assigned when not specified.
    /// </summary>
    [JsonPropertyName("size")]
    public string? Size { get; set; }

    /// <summary>
    /// Document position in the submission. If not specified, the document will be added in the order it appears in the documents array.
    /// </summary>
    [JsonPropertyName("position")]
    public int? Position { get; set; }

}
