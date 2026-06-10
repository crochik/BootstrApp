using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class GetSubmissionsResponseDataItemTemplate
{
    /// <summary>
    /// Template unique ID number.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    /// <summary>
    /// The name of the submission template.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Your application-specific unique string key to identify this template within your app.
    /// </summary>
    [JsonPropertyName("external_id")]
    public required string ExternalId { get; set; }

    /// <summary>
    /// Folder name where the template is located.
    /// </summary>
    [JsonPropertyName("folder_name")]
    public required string FolderName { get; set; }

    /// <summary>
    /// The date and time when the submission template was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; set; }

    /// <summary>
    /// The date and time when the submission template was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public required string UpdatedAt { get; set; }

}
