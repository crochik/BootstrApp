using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class CreateSubmissionFromDocxRequest
{
    /// <summary>
    /// Name of the document submission.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

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
    /// Dynamic content variables object. Variable values can be strings, numbers, arrays, objects, or HTML content used to generate styled text, paragraphs, and tables in DOCX.
    /// </summary>
    [JsonPropertyName("variables")]
    public object? Variables { get; set; }

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

    /// <summary>
    /// An optional array of template IDs to use in the submission along with the provided documents. This can be used to create multi-document submissions when some of the required documents exist within templates.
    /// </summary>
    [JsonPropertyName("template_ids")]
    public List<int>? TemplateIds { get; set; }

    [JsonPropertyName("documents")]
    public required List<CreateSubmissionFromDocxRequestDocumentsItem> Documents { get; set; }

    /// <summary>
    /// The list of submitters for the submission.
    /// </summary>
    [JsonPropertyName("submitters")]
    public required List<CreateSubmissionFromDocxRequestSubmittersItem> Submitters { get; set; }

    [JsonPropertyName("message")]
    public CreateSubmissionFromDocxRequestMessage? Message { get; set; }

    /// <summary>
    /// Set `true` to merge the documents into a single PDF file.
    /// </summary>
    [JsonPropertyName("merge_documents")]
    public bool? MergeDocuments { get; set; }

    /// <summary>
    /// Pass `false` to disable the removal of {{text}} tags from the PDF. This can be used along with transparent text tags for faster and more robust PDF processing.
    /// </summary>
    [JsonPropertyName("remove_tags")]
    public bool? RemoveTags { get; set; }

}
