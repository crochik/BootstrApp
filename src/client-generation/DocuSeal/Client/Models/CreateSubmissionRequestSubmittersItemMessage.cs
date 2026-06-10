using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionRequestSubmittersItemMessage
{
    /// <summary>
    /// Custom signature request email subject for the submitter.
    /// </summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    /// <summary>
    /// Custom signature request email body for the submitter. Can include the following variables: {{template.name}}, {{submitter.link}}, {{account.name}}.
    /// </summary>
    [JsonPropertyName("body")]
    public string? Body { get; set; }

}
