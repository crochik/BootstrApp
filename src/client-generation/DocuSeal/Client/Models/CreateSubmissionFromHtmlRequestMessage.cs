using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionFromHtmlRequestMessage
{
    /// <summary>
    /// Custom signature request email subject.
    /// </summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    /// <summary>
    /// Custom signature request email body. Can include the following variables: {{submission.name}}, {{submitter.link}}, {{account.name}}.
    /// </summary>
    [JsonPropertyName("body")]
    public string? Body { get; set; }

}
