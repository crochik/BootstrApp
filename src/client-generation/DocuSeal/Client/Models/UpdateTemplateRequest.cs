using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class UpdateTemplateRequest
{
    /// <summary>
    /// The name of the template
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The folder's name to which the template should be moved.
    /// </summary>
    [JsonPropertyName("folder_name")]
    public string? FolderName { get; set; }

    /// <summary>
    /// An array of submitter role names to update the template with.
    /// </summary>
    [JsonPropertyName("roles")]
    public List<string>? Roles { get; set; }

    /// <summary>
    /// Set `false` to unarchive template.
    /// </summary>
    [JsonPropertyName("archived")]
    public bool? Archived { get; set; }

}
