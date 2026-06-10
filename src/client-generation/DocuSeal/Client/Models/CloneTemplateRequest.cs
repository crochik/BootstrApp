using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CloneTemplateRequest
{
    /// <summary>
    /// Template name. Existing name with (Clone) suffix will be used if not specified.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The folder's name to which the template should be cloned.
    /// </summary>
    [JsonPropertyName("folder_name")]
    public string? FolderName { get; set; }

    /// <summary>
    /// Your application-specific unique string key to identify this template within your app.
    /// </summary>
    [JsonPropertyName("external_id")]
    public string? ExternalId { get; set; }

}
