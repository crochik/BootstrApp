using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

public class UpdateSubmitterRequest
{
    /// <summary>
    /// The name of the submitter.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The email address of the submitter.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>
    /// The phone number of the submitter, formatted according to the E.164 standard.
    /// </summary>
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// An object with pre-filled values for the submission. Use field names for keys of the object. For more configurations see `fields` param.
    /// </summary>
    [JsonPropertyName("values")]
    public object? Values { get; set; }

    /// <summary>
    /// Your application-specific unique string key to identify this submitter within your app.
    /// </summary>
    [JsonPropertyName("external_id")]
    public string? ExternalId { get; set; }

    /// <summary>
    /// Set `true` to re-send signature request emails.
    /// </summary>
    [JsonPropertyName("send_email")]
    public bool? SendEmail { get; set; }

    /// <summary>
    /// Set `true` to re-send signature request via phone number SMS.
    /// </summary>
    [JsonPropertyName("send_sms")]
    public bool? SendSms { get; set; }

    /// <summary>
    /// Specify Reply-To address to use in the notification emails.
    /// </summary>
    [JsonPropertyName("reply_to")]
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Pass `true` to mark submitter as completed and auto-signed via API.
    /// </summary>
    [JsonPropertyName("completed")]
    public bool? Completed { get; set; }

    /// <summary>
    /// Metadata object with additional submitter information.
    /// </summary>
    [JsonPropertyName("metadata")]
    public object? Metadata { get; set; }

    /// <summary>
    /// Submitter specific URL to redirect to after the submission completion.
    /// </summary>
    [JsonPropertyName("completed_redirect_url")]
    public string? CompletedRedirectUrl { get; set; }

    /// <summary>
    /// Set to `true` to require phone 2FA verification via a one-time code sent to the phone number in order to access the documents.
    /// </summary>
    [JsonPropertyName("require_phone_2fa")]
    public bool? RequirePhone2Fa { get; set; }

    [JsonPropertyName("message")]
    public UpdateSubmitterRequestMessage? Message { get; set; }

    /// <summary>
    /// A list of configurations for template document form fields.
    /// </summary>
    [JsonPropertyName("fields")]
    public List<UpdateSubmitterRequestFieldsItem>? Fields { get; set; }

}
