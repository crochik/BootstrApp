using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class MergeTemplateRequest
{
    /// <summary>
    /// An array of template ids to merge into a new template.
    /// </summary>
    [JsonPropertyName("template_ids")]
    public required List<int> TemplateIds { get; set; }

    /// <summary>
    /// Template name. Existing name with (Merged) suffix will be used if not specified.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The name of the folder in which the merged template should be placed.
    /// </summary>
    [JsonPropertyName("folder_name")]
    public string? FolderName { get; set; }

    /// <summary>
    /// Your application-specific unique string key to identify this template within your app.
    /// </summary>
    [JsonPropertyName("external_id")]
    public string? ExternalId { get; set; }

    /// <summary>
    /// set to `true` to make the template available via a shared link. This will allow anyone with the link to create a submission from this template.
    /// </summary>
    [JsonPropertyName("shared_link")]
    public bool? SharedLink { get; set; }

    /// <summary>
    /// An array of submitter role names to be used in the merged template.
    /// </summary>
    [JsonPropertyName("roles")]
    public List<string>? Roles { get; set; }

}
