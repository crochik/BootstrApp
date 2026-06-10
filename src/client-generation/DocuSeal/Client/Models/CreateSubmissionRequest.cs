using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionRequest
{
    /// <summary>
    /// The unique identifier of the template. Document template forms can be created via the Web UI, <a href="https://www.docuseal.com/guides/use-embedded-text-field-tags-in-the-pdf-to-create-a-fillable-form" class="link">PDF and DOCX API</a>, or <a href="https://www.docuseal.com/guides/create-pdf-document-fillable-form-with-html-api" class="link">HTML API</a>.
    /// </summary>
    [JsonPropertyName("template_id")]
    public required int TemplateId { get; set; }

    /// <summary>
    /// Set `false` to disable signature request emails sending.
    /// </summary>
    [JsonPropertyName("send_email")]
    public bool? SendEmail { get; set; }

    /// <summary>
    /// Set `true` to send signature request via phone number and SMS.
    /// </summary>
    [JsonPropertyName("send_sms")]
    public bool? SendSms { get; set; }

    /// <summary>
    /// Pass 'random' to send signature request emails to all parties right away. The order is 'preserved' by default so the second party will receive a signature request email only after the document is signed by the first party.
    /// </summary>
    [JsonPropertyName("order")]
    public string? Order { get; set; }

    /// <summary>
    /// Specify URL to redirect to after the submission completion.
    /// </summary>
    [JsonPropertyName("completed_redirect_url")]
    public string? CompletedRedirectUrl { get; set; }

    /// <summary>
    /// Specify BCC address to send signed documents to after the completion.
    /// </summary>
    [JsonPropertyName("bcc_completed")]
    public string? BccCompleted { get; set; }

    /// <summary>
    /// Specify Reply-To address to use in the notification emails.
    /// </summary>
    [JsonPropertyName("reply_to")]
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Specify the expiration date and time after which the submission becomes unavailable for signature.
    /// </summary>
    [JsonPropertyName("expire_at")]
    public string? ExpireAt { get; set; }

    [JsonPropertyName("message")]
    public CreateSubmissionRequestMessage? Message { get; set; }

    /// <summary>
    /// The list of submitters for the submission.
    /// </summary>
    [JsonPropertyName("submitters")]
    public required List<CreateSubmissionRequestSubmittersItem> Submitters { get; set; }

}
