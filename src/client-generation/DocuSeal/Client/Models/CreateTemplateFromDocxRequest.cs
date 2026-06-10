using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateTemplateFromDocxRequest
{
    /// <summary>
    /// Name of the template
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Your application-specific unique string key to identify this template within your app. Existing template with specified `external_id` will be updated with a new document.
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

    [JsonPropertyName("documents")]
    public required List<CreateTemplateFromDocxRequestDocumentsItem> Documents { get; set; }

}
