using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Extensions;

namespace TrustedForm;

/// <summary>
/// CertificateOperationRequest
/// </summary>
public class CertificateOperationRequest
{
    /// <summary>
    /// Gets or Sets MatchLead
    /// </summary>
    [JsonProperty("match_lead")]
    public MatchLeadPhoneEmailParameters MatchLead { get; set; }

    /// <summary>
    /// Gets or Sets Retain
    /// </summary>
    [JsonProperty("retain")]
    public RetainParameters Retain { get; set; }

    /// <summary>
    /// Gets or Sets Insights
    /// </summary>
    [JsonProperty("insights")]
    public InsightsParameters Insights { get; set; }

    /// <summary>
    /// Gets or Sets Verify
    /// </summary>
    [JsonProperty("verify")]
    public VerifyParameters Verify { get; set; }
}

public class VerifyParameters
{
    /// <summary>
    /// Defines OptInTypesAllowed
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum OptInTypesAllowedEnum
    {
        /// <summary>
        /// Enum Manual for value: manual
        /// </summary>
        [EnumMember(Value = "manual")] Manual = 1,

        /// <summary>
        /// Enum PreSelected for value: pre-selected
        /// </summary>
        [EnumMember(Value = "pre-selected")] PreSelected = 2,

        /// <summary>
        /// Enum NonInteractive for value: non-interactive
        /// </summary>
        [EnumMember(Value = "non-interactive")]
        NonInteractive = 3
    }

    /// <summary>
    /// The name of the legal entity for an advertiser, used to determine if they were given consent in a one-to-one manner. Normalized to be case-insensitive, ignore extra spaces, and omit non-alphanumeric characters (e.g., “Acme Inc.” and “acme inc” are the same). This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided in either. 
    /// </summary>
    /// <value>The name of the legal entity for an advertiser, used to determine if they were given consent in a one-to-one manner. Normalized to be case-insensitive, ignore extra spaces, and omit non-alphanumeric characters (e.g., “Acme Inc.” and “acme inc” are the same). This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided in either. </value>
    /*
    <example>Acme Inc.</example>
    */
    [JsonProperty("advertiser_name")]
    public string AdvertiserName { get; set; }

    /// <summary>
    /// The number indicating the minimum contrast ratio required between the consent language text and background. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided in either. 
    /// </summary>
    /// <value>The number indicating the minimum contrast ratio required between the consent language text and background. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided in either. </value>
    /*
    <example>7</example>
    */
    [JsonProperty("min_contrast_ratio_required")]
    public decimal MinContrastRatioRequired { get; set; }

    /// <summary>
    /// The number indicating the minimum font size required for the consent language. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided in either. 
    /// </summary>
    /// <value>The number indicating the minimum font size required for the consent language. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided in either. </value>
    /*
    <example>16</example>
    */
    [JsonProperty("min_font_size_px_required")]
    public decimal MinFontSizePxRequired { get; set; }

    /// <summary>
    /// An array of strings that lists the opt-in types that are allowed. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided through either method. The array can include one or more of the following values:  - &#x60;manual&#x60;: The consumer actively checked a box to provide consent. - &#x60;pre-selected&#x60;: An opt-in field was selected by default, without explicit action from the consumer. - &#x60;non-interactive&#x60;: In the absence of an opt-in field, the consumer gave consent by submitting the form.  This field is used to define which opt-in types are considered valid. 
    /// </summary>
    /// <value>An array of strings that lists the opt-in types that are allowed. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided through either method. The array can include one or more of the following values:  - &#x60;manual&#x60;: The consumer actively checked a box to provide consent. - &#x60;pre-selected&#x60;: An opt-in field was selected by default, without explicit action from the consumer. - &#x60;non-interactive&#x60;: In the absence of an opt-in field, the consumer gave consent by submitting the form.  This field is used to define which opt-in types are considered valid. </value>
    [JsonProperty("opt_in_types_allowed")]
    public List<VerifyParameters.OptInTypesAllowedEnum> OptInTypesAllowed { get; set; }
}

public class InsightsParameters
{
    /// <summary>
    /// Defines Properties
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PropertiesEnum
    {
        /// <summary>
        /// Enum AgeSeconds for value: age_seconds
        /// </summary>
        [EnumMember(Value = "age_seconds")] AgeSeconds = 1,

        /// <summary>
        /// Enum ApproxIpGeo for value: approx_ip_geo
        /// </summary>
        [EnumMember(Value = "approx_ip_geo")] ApproxIpGeo = 2,

        /// <summary>
        /// Enum BotDetected for value: bot_detected
        /// </summary>
        [EnumMember(Value = "bot_detected")] BotDetected = 3,

        /// <summary>
        /// Enum ConfirmedOwner for value: confirmed_owner
        /// </summary>
        [EnumMember(Value = "confirmed_owner")]
        ConfirmedOwner = 4,

        /// <summary>
        /// Enum CreatedAt for value: created_at
        /// </summary>
        [EnumMember(Value = "created_at")] CreatedAt = 5,

        /// <summary>
        /// Enum Domain for value: domain
        /// </summary>
        [EnumMember(Value = "domain")] Domain = 6,

        /// <summary>
        /// Enum ExpiresAt for value: expires_at
        /// </summary>
        [EnumMember(Value = "expires_at")] ExpiresAt = 7,

        /// <summary>
        /// Enum FormInputKpm for value: form_input_kpm
        /// </summary>
        [EnumMember(Value = "form_input_kpm")] FormInputKpm = 8,

        /// <summary>
        /// Enum FormInputMethod for value: form_input_method
        /// </summary>
        [EnumMember(Value = "form_input_method")]
        FormInputMethod = 9,

        /// <summary>
        /// Enum FormInputWpm for value: form_input_wpm
        /// </summary>
        [EnumMember(Value = "form_input_wpm")] FormInputWpm = 10,

        /// <summary>
        /// Enum Ip for value: ip
        /// </summary>
        [EnumMember(Value = "ip")] Ip = 11,

        /// <summary>
        /// Enum IsFramed for value: is_framed
        /// </summary>
        [EnumMember(Value = "is_framed")] IsFramed = 12,

        /// <summary>
        /// Enum IsMasked for value: is_masked
        /// </summary>
        [EnumMember(Value = "is_masked")] IsMasked = 13,

        /// <summary>
        /// Enum NumSensitiveContentElements for value: num_sensitive_content_elements
        /// </summary>
        [EnumMember(Value = "num_sensitive_content_elements")]
        NumSensitiveContentElements = 14,

        /// <summary>
        /// Enum NumSensitiveFormElements for value: num_sensitive_form_elements
        /// </summary>
        [EnumMember(Value = "num_sensitive_form_elements")]
        NumSensitiveFormElements = 15,

        /// <summary>
        /// Enum Os for value: os
        /// </summary>
        [EnumMember(Value = "os")] Os = 16,

        /// <summary>
        /// Enum PageUrl for value: page_url
        /// </summary>
        [EnumMember(Value = "page_url")] PageUrl = 17,

        /// <summary>
        /// Enum ParentPageUrl for value: parent_page_url
        /// </summary>
        [EnumMember(Value = "parent_page_url")]
        ParentPageUrl = 18,

        /// <summary>
        /// Enum SecondsOnPage for value: seconds_on_page
        /// </summary>
        [EnumMember(Value = "seconds_on_page")]
        SecondsOnPage = 19
    }

    /// <summary>
    ///         A list of the Insights data points you would like to be returned in         the response. Some &#x60;properties&#x60; are not          compatible with all certificate types and will return a null value.         Only contracted &#x60;properties&#x60; are          available to query. Your account will only be charged for properties         that are returned.          See InsightsResult for &#x60;form_input_method&#x60; values. 
    /// </summary>
    /// <value>        A list of the Insights data points you would like to be returned in         the response. Some &#x60;properties&#x60; are not          compatible with all certificate types and will return a null value.         Only contracted &#x60;properties&#x60; are          available to query. Your account will only be charged for properties         that are returned.          See InsightsResult for &#x60;form_input_method&#x60; values. </value>
    [JsonProperty("properties")]
    public List<InsightsParameters.PropertiesEnum> Properties { get; set; }

    // /// <summary>
    // /// Gets or Sets Scans
    // /// </summary>
    // [JsonProperty("scans")]
    // public InsightsParametersScans Scans { get; set; }
}

// public class InsightsParametersScans
// {
//     /// <summary>
//     /// Use this parameter to designate a delimiter to use when wrapping wildcards. Your choice of delimiter must be homogeneous (i.e. the beginning and end are the same character(s)), such as |, &#x3D;&#x3D;, or |||. 
//     /// </summary>
//     /// <value>Use this parameter to designate a delimiter to use when wrapping wildcards. Your choice of delimiter must be homogeneous (i.e. the beginning and end are the same character(s)), such as |, &#x3D;&#x3D;, or |||. </value>
//     [JsonProperty("delimiter")]
//     public string Delimiter { get; set; }
//
//     /// <summary>
//     /// Gets or Sets Forbidden
//     /// </summary>
//     [JsonProperty("forbidden")]
//     public InsightsParametersScansForbidden Forbidden { get; set; }
//
//     /// <summary>
//     /// Gets or Sets Required
//     /// </summary>
//     [JsonProperty("required")]
//     public InsightsParametersScansRequired Required { get; set; }
// }

public class MatchLeadPhoneEmailParameters
{
    /// <summary>
    /// The email of the consumer you believe was recorded in the certificate. Optionally you can hash the value using a SHA1 hash function in place of providing the unhashed value
    /// </summary>
    /// <value>The email of the consumer you believe was recorded in the certificate. Optionally you can hash the value using a SHA1 hash function in place of providing the unhashed value</value>
    [JsonProperty("email")]
    public string Email { get; set; }

    /// <summary>
    /// The phone number of the consumer you believe was recorded in the certificate. Optionally you can hash the value using a SHA1 hash function in place of providing the unhashed value.
    /// </summary>
    /// <value>The phone number of the consumer you believe was recorded in the certificate. Optionally you can hash the value using a SHA1 hash function in place of providing the unhashed value.</value>
    [JsonProperty("phone")]
    public string Phone { get; set; }
}

public class RetainParameters
{
    /// <summary>
    /// Any text that may help you identify the lead associated with the certificate such as a unique lead identifier or URL pointing to the lead in another system. This value will be displayed in your copy of the certificate for your future reference 
    /// </summary>
    /// <value>Any text that may help you identify the lead associated with the certificate such as a unique lead identifier or URL pointing to the lead in another system. This value will be displayed in your copy of the certificate for your future reference </value>
    /*
    <example>1128238382829</example>
    */
    [JsonProperty("reference")]
    public string Reference { get; set; }

    /// <summary>
    /// When retaining a certificate, you can optionally pass the vendor name. This is intended for tracking the name of the company that provided the lead associated with the certificate. TrustedForm will record this value on the certificate stored in your account. Other accounts cannot see this value. When you use TrustedForm reporting, you can easily filter or group by vendor. 
    /// </summary>
    /// <value>When retaining a certificate, you can optionally pass the vendor name. This is intended for tracking the name of the company that provided the lead associated with the certificate. TrustedForm will record this value on the certificate stored in your account. Other accounts cannot see this value. When you use TrustedForm reporting, you can easily filter or group by vendor. </value>
    /*
    <example>Acme Co.</example>
    */
    [JsonProperty("vendor")]
    public string Vendor { get; set; }
}

public static class CertificateOperationRequestExtensions
{
    public static Task<CertificateOperationResponse> Execute(this CertificateOperationRequest body, HttpClient client, string apiKey, string id)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"API:{apiKey}"));
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"https://cert.trustedform.com/{id}");
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.Add("Authorization", $"Basic {credentials}");

        var json = JsonConvert.SerializeObject(body, Formatting.Indented, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        });
        requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return client.SendAsync<CertificateOperationResponse>(requestMessage);
    }
}