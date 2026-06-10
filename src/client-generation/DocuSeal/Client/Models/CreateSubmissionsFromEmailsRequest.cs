using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionsFromEmailsRequest
{
    /// <summary>
    /// The unique identifier of the template.
    /// </summary>
    [JsonPropertyName("template_id")]
    public required int TemplateId { get; set; }

    /// <summary>
    /// A comma-separated list of email addresses to send the submission to.
    /// </summary>
    [JsonPropertyName("emails")]
    public required string Emails { get; set; }

    /// <summary>
    /// Set `false` to disable signature request emails sending.
    /// </summary>
    [JsonPropertyName("send_email")]
    public bool? SendEmail { get; set; }

    [JsonPropertyName("message")]
    public CreateSubmissionsFromEmailsRequestMessage? Message { get; set; }

}
