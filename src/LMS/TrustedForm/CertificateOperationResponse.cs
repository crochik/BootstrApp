using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TrustedForm;

public class CertificateOperationResponse
{
    /// <summary>
    ///   The overall outcome of executed operations. Indicates whether the call succeeded, failed, or resulted in an error.   Best practice is to use this property to determine if you should purchase a lead. A &#x60;failure&#x60; or &#x60;error&#x60;   indicate that the lead should not be contacted. The reason for failure or error is revealed in the &#x60;reason&#x60; property. 
    /// </summary>
    /// <value>  The overall outcome of executed operations. Indicates whether the call succeeded, failed, or resulted in an error.   Best practice is to use this property to determine if you should purchase a lead. A &#x60;failure&#x60; or &#x60;error&#x60;   indicate that the lead should not be contacted. The reason for failure or error is revealed in the &#x60;reason&#x60; property. </value>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum OutcomeEnum
    {
        /// <summary>
        /// Enum Success for value: success
        /// </summary>
        [EnumMember(Value = "success")] Success = 1,

        /// <summary>
        /// Enum Failure for value: failure
        /// </summary>
        [EnumMember(Value = "failure")] Failure = 2,

        /// <summary>
        /// Enum Error for value: error
        /// </summary>
        [EnumMember(Value = "error")] Error = 3
    }

    /// <summary>
    ///   The overall outcome of executed operations. Indicates whether the call succeeded, failed, or resulted in an error.   Best practice is to use this property to determine if you should purchase a lead. A &#x60;failure&#x60; or &#x60;error&#x60;   indicate that the lead should not be contacted. The reason for failure or error is revealed in the &#x60;reason&#x60; property. 
    /// </summary>
    /// <value>  The overall outcome of executed operations. Indicates whether the call succeeded, failed, or resulted in an error.   Best practice is to use this property to determine if you should purchase a lead. A &#x60;failure&#x60; or &#x60;error&#x60;   indicate that the lead should not be contacted. The reason for failure or error is revealed in the &#x60;reason&#x60; property. </value>
    /*
    <example>success</example>
    */
    [JsonProperty("outcome")]
    public OutcomeEnum? Outcome { get; set; }

    /// <summary>
    /// Explanation for a &#x60;failure&#x60; or &#x60;error&#x60;, otherwise &#x60;null&#x60;
    /// </summary>
    /// <value>Explanation for a &#x60;failure&#x60; or &#x60;error&#x60;, otherwise &#x60;null&#x60;</value>
    /*
    <example>null</example>
    */
    [JsonProperty("reason")]
    public string Reason { get; set; }

    /// <summary>
    /// Gets or Sets MatchLead
    /// </summary>
    [JsonProperty("match_lead")]
    public MatchLeadResult MatchLead { get; set; }

    /// <summary>
    /// Gets or Sets Retain
    /// </summary>
    [JsonProperty("retain")]
    public RetainResult Retain { get; set; }

    /// <summary>
    /// Gets or Sets Insights
    /// </summary>
    [JsonProperty("insights")]
    public InsightsResult Insights { get; set; }

    /// <summary>
    /// Gets or Sets Verify
    /// </summary>
    [JsonProperty("verify")]
    public VerifyResult Verify { get; set; }
}

public class VerifyResult
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
    ///   The legal name of the advertiser used to perform the 1:1 consent language check. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided in either. 
    /// </summary>
    /// <value>  The legal name of the advertiser used to perform the 1:1 consent language check. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided in either. </value>
    [JsonProperty("advertiser_name")]
    public string AdvertiserName { get; set; }

    /// <summary>
    /// A list of the consent languages detected within the certificate
    /// </summary>
    /// <value>A list of the consent languages detected within the certificate</value>
    [JsonProperty("languages")]
    public List<VerifyResultLanguagesInner> Languages { get; set; }

    /// <summary>
    ///   The number indicating the minimum contrast ratio required between the consent language text and background. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided in either. 
    /// </summary>
    /// <value>  The number indicating the minimum contrast ratio required between the consent language text and background. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided in either. </value>
    [JsonProperty("min_contrast_ratio_required")]
    public decimal? MinContrastRatioRequired { get; set; }

    /// <summary>
    ///   The number indicating the minimum font size required for the consent language. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided in either. 
    /// </summary>
    /// <value>  The number indicating the minimum font size required for the consent language. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided in either. </value>
    [JsonProperty("min_font_size_px_required")]
    public decimal? MinFontSizePxRequired { get; set; }

    /// <summary>
    ///   An array of strings that lists the opt-in types that are allowed. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided through either method. 
    /// </summary>
    /// <value>  An array of strings that lists the opt-in types that are allowed. This value can be passed in the request or set via the [verification criteria page](https://app.trustedform.com/verification_criteria) and will appear in the response if provided through either method. </value>
    [JsonProperty("opt_in_types_allowed")]
    public List<VerifyResult.OptInTypesAllowedEnum> OptInTypesAllowed { get; set; }

    /// <summary>
    /// Gets or Sets Result
    /// </summary>
    [JsonProperty("result")]
    public VerifyResultResult Result { get; set; }
}

public class VerifyResultLanguagesInner
{
    /// <summary>
    /// The text of a consent language found within the certificate
    /// </summary>
    /// <value>The text of a consent language found within the certificate</value>
    /*
    <example>By clicking on the &#39;Submit&#39; button below, I consent to be contacted</example>
    */
    [JsonProperty("text")]
    public string Text { get; set; }
}

public class VerifyResultResult
{
    /// <summary>
    /// A boolean indicating whether the form was successfully submitted by the consumer. true means a form submission was detected, while false indicates that the form was abandoned before submission. This field will only influence the one_to_one check result, not the overall Verify outcome.
    /// </summary>
    /// <value>A boolean indicating whether the form was successfully submitted by the consumer. true means a form submission was detected, while false indicates that the form was abandoned before submission. This field will only influence the one_to_one check result, not the overall Verify outcome.</value>
    [JsonProperty("form_submitted")]
    public bool? FormSubmitted { get; set; }

    /// <summary>
    /// A boolean indicating if any of the consent languages found have been approved in your account’s consent language manager.
    /// </summary>
    /// <value>A boolean indicating if any of the consent languages found have been approved in your account’s consent language manager.</value>
    [JsonProperty("language_approved")]
    public bool? LanguageApproved { get; set; }

    /// <summary>
    /// A boolean indicating whether the contrast ratio between the consent language text and background meets or exceeds the required minimum contrast ratio.  true means the contrast requirement was satisfied, while false means it was insufficient. A null value is returned when the min_contrast_ratio_required is missing, and the check could not be performed.
    /// </summary>
    /// <value>A boolean indicating whether the contrast ratio between the consent language text and background meets or exceeds the required minimum contrast ratio.  true means the contrast requirement was satisfied, while false means it was insufficient. A null value is returned when the min_contrast_ratio_required is missing, and the check could not be performed.</value>
    [JsonProperty("min_contrast_ratio_satisfied")]
    public bool? MinContrastRatioSatisfied { get; set; }

    /// <summary>
    /// A boolean indicating whether the consent language meets or exceeds the required minimum font size. true means the font size requirement was satisfied, while false indicates it was not. A null value is returned when the min_font_size_px_required is missing, and the check could not be performed.
    /// </summary>
    /// <value>A boolean indicating whether the consent language meets or exceeds the required minimum font size. true means the font size requirement was satisfied, while false indicates it was not. A null value is returned when the min_font_size_px_required is missing, and the check could not be performed.</value>
    [JsonProperty("min_font_size_px_satisfied")]
    public bool? MinFontSizePxSatisfied { get; set; }

    /// <summary>
    /// A boolean indicating if the cert structure satisfied the requirements for 1:1 consent. You must pass the &#x60;advertiser_name&#x60; for the check to be performed. A &#x60;null&#x60; value is returned when consent tags were not used, or when the &#x60;advertiser_name&#x60; is missing, and the consent check could not be performed.
    /// </summary>
    /// <value>A boolean indicating if the cert structure satisfied the requirements for 1:1 consent. You must pass the &#x60;advertiser_name&#x60; for the check to be performed. A &#x60;null&#x60; value is returned when consent tags were not used, or when the &#x60;advertiser_name&#x60; is missing, and the consent check could not be performed.</value>
    [JsonProperty("one_to_one")]
    public bool? OneToOne { get; set; }

    /// <summary>
    /// A boolean indicating whether all the opt-in types on the form match one or more of the allowed opt-in types specified in the opt_in_types_allowed parameter. true means all opt-in types on the form meet the specified criteria.  false means at least one opt-in type on the form does not match any of the allowed types. A null value is returned when the opt_in_types_allowed is missing, and the check could not be performed. 
    /// </summary>
    /// <value>A boolean indicating whether all the opt-in types on the form match one or more of the allowed opt-in types specified in the opt_in_types_allowed parameter. true means all opt-in types on the form meet the specified criteria.  false means at least one opt-in type on the form does not match any of the allowed types. A null value is returned when the opt_in_types_allowed is missing, and the check could not be performed. </value>
    [JsonProperty("opt_in_types_satisfied")]
    public bool? OptInTypesSatisfied { get; set; }

    /// <summary>
    /// A boolean indicating whether all Verify checks succeeded.
    /// </summary>
    /// <value>A boolean indicating whether all Verify checks succeeded.</value>
    [JsonProperty("success")]
    public bool? Success { get; set; }
}

public class InsightsResult
{
    /// <summary>
    /// Gets or Sets Properties
    /// </summary>
    [JsonProperty("properties")]
    public InsightsResultProperties Properties { get; set; }

    // /// <summary>
    // /// Gets or Sets Scans
    // /// </summary>
    // [JsonProperty("scans")]
    // public ScansResult Scans { get; set; }
}

// public class ScansResult
// {
//     /// <summary>
//     /// The parameter provided in the request that was used as the delimiter to identify wildcards during the page scan process.
//     /// </summary>
//     /// <value>The parameter provided in the request that was used as the delimiter to identify wildcards during the page scan process.</value>
//     [JsonProperty("delimiter")]
//     public string Delimiter { get; set; }
//
//     /// <summary>
//     /// Gets or Sets Forbidden
//     /// </summary>
//     [JsonProperty("forbidden")]
//     public ScansResultForbidden Forbidden { get; set; }
//
//     /// <summary>
//     /// Gets or Sets Required
//     /// </summary>
//     [JsonProperty("required")]
//     public ScansResultRequired Required { get; set; }
//
//     /// <summary>
//     /// Gets or Sets Result
//     /// </summary>
//     [JsonProperty("result")]
//     public ScansResultResult Result { get; set; }
// }


public class InsightsResultProperties 
{
    /// <summary>
    /// Defines FormInputMethod
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum FormInputMethodEnum
    {
        /// <summary>
        /// Enum Autofill for value: autofill
        /// </summary>
        [EnumMember(Value = "autofill")] Autofill = 1,

        /// <summary>
        /// Enum Paste for value: paste
        /// </summary>
        [EnumMember(Value = "paste")] Paste = 2,

        /// <summary>
        /// Enum Typing for value: typing
        /// </summary>
        [EnumMember(Value = "typing")] Typing = 3,

        /// <summary>
        /// Enum PrePopulated for value: pre-populated
        /// </summary>
        [EnumMember(Value = "pre-populated")] PrePopulated = 4
    }

    /// <summary>
    /// Number of seconds since the last user interaction with the certificate.
    /// </summary>
    /// <value>Number of seconds since the last user interaction with the certificate.</value>
    [JsonProperty("age_seconds")]
    public int? AgeSeconds { get; set; }

    /// <summary>
    /// Gets or Sets ApproxIpGeo
    /// </summary>
    [JsonProperty("approx_ip_geo")]
    public MobileInsightsResultPropertiesApproxIpGeo ApproxIpGeo { get; set; }

    /// <summary>
    /// A determination of whether the events documented were likely produced by a non-human entity based on ActiveProspect’s proprietary algorithms.
    /// </summary>
    /// <value>A determination of whether the events documented were likely produced by a non-human entity based on ActiveProspect’s proprietary algorithms.</value>
    [JsonProperty("bot_detected")]
    public bool? BotDetected { get; set; }

    /// <summary>
    /// Gets or Sets Browser
    /// </summary>
    [JsonProperty("browser")]
    public WebInsightsResultPropertiesBrowser Browser { get; set; }

    /// <summary>
    /// This field indicates the verified owner of the TrustedForm certificate.  The value can be one of the following: - &#x60;&#x60;\&quot;No Verified ActiveProspect Account Identified\&quot;&#x60;&#x60; — no account has been confirmed as the owner of this certificate. - &#x60;&#x60;\&quot;ActiveProspect Verified Account\&quot;&#x60;&#x60; — the certificate has been confirmed to belong to an account verified by ActiveProspect, but the specific account name is not disclosed as the account is not a connected partner or has not granted permission to confirm ownership. - &#x60;&#x60;\&quot;&lt;account name&gt;\&quot;&#x60;&#x60; — the name of the verified account that owns the certificate and has granted permission to confirm ownership.  To manage permissions, visit the [Connections](https://account.activeprospect.com/connections/invitations/new) page. You can invite a new partner to connect, or request the necessary permissions from an existing connection. Once granted, ownership information can be shared transparently. 
    /// </summary>
    /// <value>This field indicates the verified owner of the TrustedForm certificate.  The value can be one of the following: - &#x60;&#x60;\&quot;No Verified ActiveProspect Account Identified\&quot;&#x60;&#x60; — no account has been confirmed as the owner of this certificate. - &#x60;&#x60;\&quot;ActiveProspect Verified Account\&quot;&#x60;&#x60; — the certificate has been confirmed to belong to an account verified by ActiveProspect, but the specific account name is not disclosed as the account is not a connected partner or has not granted permission to confirm ownership. - &#x60;&#x60;\&quot;&lt;account name&gt;\&quot;&#x60;&#x60; — the name of the verified account that owns the certificate and has granted permission to confirm ownership.  To manage permissions, visit the [Connections](https://account.activeprospect.com/connections/invitations/new) page. You can invite a new partner to connect, or request the necessary permissions from an existing connection. Once granted, ownership information can be shared transparently. </value>
    [JsonProperty("confirmed_owner")]
    public string ConfirmedOwner { get; set; }

    /// <summary>
    /// The UTC ISO8601 formatted date and time when TrustedForm Certify was loaded.
    /// </summary>
    /// <value>The UTC ISO8601 formatted date and time when TrustedForm Certify was loaded.</value>
    [JsonProperty("created_at")]
    public string CreatedAt { get; set; }

    /// <summary>
    /// The domain displayed to the consumer during the page visit.
    /// </summary>
    /// <value>The domain displayed to the consumer during the page visit.</value>
    [JsonProperty("domain")]
    public string Domain { get; set; }

    /// <summary>
    /// The UTC ISO8601 formatted date and time when the certificate will expire.
    /// </summary>
    /// <value>The UTC ISO8601 formatted date and time when the certificate will expire.</value>
    [JsonProperty("expires_at")]
    public string ExpiresAt { get; set; }

    /// <summary>
    /// The average number of keystrokes per minute based on the consumer&#39;s rate of form input.
    /// </summary>
    /// <value>The average number of keystrokes per minute based on the consumer&#39;s rate of form input.</value>
    [JsonProperty("form_input_kpm")]
    public decimal? FormInputKpm { get; set; }

    /// <summary>
    /// The detected input method(s) the consumer used to fill out the form.
    /// </summary>
    /// <value>The detected input method(s) the consumer used to fill out the form.</value>
    [JsonProperty("form_input_method")]
    public List<InsightsResultProperties.FormInputMethodEnum> FormInputMethod { get; set; }

    /// <summary>
    /// The approximate number of words per minute calculated by using the form_input_kpm and assuming five characters represent a word.
    /// </summary>
    /// <value>The approximate number of words per minute calculated by using the form_input_kpm and assuming five characters represent a word.</value>
    [JsonProperty("form_input_wpm")]
    public decimal? FormInputWpm { get; set; }

    /// <summary>
    /// The consumer&#39;s public IP address.
    /// </summary>
    /// <value>The consumer&#39;s public IP address.</value>
    [JsonProperty("ip")]
    public string Ip { get; set; }

    /// <summary>
    /// A boolean indicating that the form was displayed within an iframe.
    /// </summary>
    /// <value>A boolean indicating that the form was displayed within an iframe.</value>
    [JsonProperty("is_framed")]
    public bool? IsFramed { get; set; }

    /// <summary>
    /// A boolean indicating if the certificate is masked and does not show source information nor a session replay.
    /// </summary>
    /// <value>A boolean indicating if the certificate is masked and does not show source information nor a session replay.</value>
    [JsonProperty("is_masked")]
    public bool? IsMasked { get; set; }

    /// <summary>
    /// Count of how many content elements (e.g. img, div) are marked sensitive and hidden from the session replay.
    /// </summary>
    /// <value>Count of how many content elements (e.g. img, div) are marked sensitive and hidden from the session replay.</value>
    [JsonProperty("num_sensitive_content_elements")]
    public decimal? NumSensitiveContentElements { get; set; }

    /// <summary>
    /// Count of how many form elements (e.g. input, textarea) are marked sensitive and hidden from the session replay.
    /// </summary>
    /// <value>Count of how many form elements (e.g. input, textarea) are marked sensitive and hidden from the session replay.</value>
    [JsonProperty("num_sensitive_form_elements")]
    public decimal? NumSensitiveFormElements { get; set; }

    /// <summary>
    /// Gets or Sets Os
    /// </summary>
    [JsonProperty("os")]
    public MobileInsightsResultPropertiesOs Os { get; set; }

    /// <summary>
    /// The URL of the page hosting TrustedForm Certify.
    /// </summary>
    /// <value>The URL of the page hosting TrustedForm Certify.</value>
    [JsonProperty("page_url")]
    public string PageUrl { get; set; }

    /// <summary>
    /// The parent URL of the page hosting TrustedForm Certify, if framed.
    /// </summary>
    /// <value>The parent URL of the page hosting TrustedForm Certify, if framed.</value>
    [JsonProperty("parent_page_url")]
    public string ParentPageUrl { get; set; }

    /// <summary>
    /// The time in seconds between when TrustedForm Certify was loaded and when the most recent cert event was received.
    /// </summary>
    /// <value>The time in seconds between when TrustedForm Certify was loaded and when the most recent cert event was received.</value>
    [JsonProperty("seconds_on_page")]
    public decimal? SecondsOnPage { get; set; }
}

public class WebInsightsResultPropertiesBrowser
{
    /// <summary>
    /// A human-friendly version of the browser parsed from the user-agent.
    /// </summary>
    /// <value>A human-friendly version of the browser parsed from the user-agent.</value>
    [JsonProperty("full")]
    public string Full { get; set; }

    /// <summary>
    /// The browser&#39;s name.
    /// </summary>
    /// <value>The browser&#39;s name.</value>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>
    /// The consumer&#39;s browser user-agent.
    /// </summary>
    /// <value>The consumer&#39;s browser user-agent.</value>
    [JsonProperty("user_agent")]
    public string UserAgent { get; set; }

    /// <summary>
    /// Gets or Sets VarVersion
    /// </summary>
    [JsonProperty("version")]
    public WebInsightsResultPropertiesBrowserVersion VarVersion { get; set; }
}

public class WebInsightsResultPropertiesBrowserVersion
{
    /// <summary>
    /// A string containing the full version.
    /// </summary>
    /// <value>A string containing the full version.</value>
    [JsonProperty("full")]
    public string Full { get; set; }

    /// <summary>
    /// A string containing the major version.
    /// </summary>
    /// <value>A string containing the major version.</value>
    [JsonProperty("major")]
    public string Major { get; set; }

    /// <summary>
    /// A string containing the minor version.
    /// </summary>
    /// <value>A string containing the minor version.</value>
    [JsonProperty("minor")]
    public string Minor { get; set; }

    /// <summary>
    /// A string containing the patch version.
    /// </summary>
    /// <value>A string containing the patch version.</value>
    [JsonProperty("patch")]
    public string Patch { get; set; }
}

public class MobileInsightsResultPropertiesOs
{
    /// <summary>
    /// A human-friendly version of the operating system information parsed from the user-agent.
    /// </summary>
    /// <value>A human-friendly version of the operating system information parsed from the user-agent.</value>
    [JsonProperty("full")]
    public string Full { get; set; }

    /// <summary>
    /// A boolean indicating that the form was filled out on a mobile device or tablet, based on the user-agent.
    /// </summary>
    /// <value>A boolean indicating that the form was filled out on a mobile device or tablet, based on the user-agent.</value>
    [JsonProperty("is_mobile")]
    public bool? IsMobile { get; set; }

    /// <summary>
    /// The operating system&#39;s name.
    /// </summary>
    /// <value>The operating system&#39;s name.</value>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or Sets VarVersion
    /// </summary>
    [JsonProperty("version")]
    public MobileInsightsResultPropertiesOsVersion VarVersion { get; set; }
}

public class MobileInsightsResultPropertiesOsVersion
{
    /// <summary>
    /// A string containing the version of the consumer&#39;s operating system.
    /// </summary>
    /// <value>A string containing the version of the consumer&#39;s operating system.</value>
    [JsonProperty("full")]
    public string Full { get; set; }

    /// <summary>
    /// A string containing the major version of the consumer&#39;s operating system.
    /// </summary>
    /// <value>A string containing the major version of the consumer&#39;s operating system.</value>
    [JsonProperty("major")]
    public string Major { get; set; }

    /// <summary>
    /// A string containing the minor version of the consumer&#39;s operating system.
    /// </summary>
    /// <value>A string containing the minor version of the consumer&#39;s operating system.</value>
    [JsonProperty("minor")]
    public string Minor { get; set; }

    /// <summary>
    /// A string containing the patch version of the consumer&#39;s operating system.
    /// </summary>
    /// <value>A string containing the patch version of the consumer&#39;s operating system.</value>
    [JsonProperty("patch")]
    public string Patch { get; set; }
}

public class MobileInsightsResultPropertiesApproxIpGeo
{
    /// <summary>
    /// City name based on consumer&#39;s public IP address.
    /// </summary>
    /// <value>City name based on consumer&#39;s public IP address.</value>
    [JsonProperty("city")]
    public string City { get; set; }

    /// <summary>
    /// Country code based on consumer&#39;s public IP address
    /// </summary>
    /// <value>Country code based on consumer&#39;s public IP address</value>
    [JsonProperty("country_code")]
    public string CountryCode { get; set; }

    /// <summary>
    /// Latitude based on consumer&#39;s public IP address.
    /// </summary>
    /// <value>Latitude based on consumer&#39;s public IP address.</value>
    [JsonProperty("lat")]
    public decimal? Lat { get; set; }

    /// <summary>
    /// Longitude based on consumer&#39;s public IP address.
    /// </summary>
    /// <value>Longitude based on consumer&#39;s public IP address.</value>
    [JsonProperty("lon")]
    public decimal? Lon { get; set; }

    /// <summary>
    /// Mailing address postal code based on consumer&#39;s public IP address.
    /// </summary>
    /// <value>Mailing address postal code based on consumer&#39;s public IP address.</value>
    [JsonProperty("postal_code")]
    public string PostalCode { get; set; }

    /// <summary>
    /// State/Province or Political Subdivision abbreviation based on consumer&#39;s public IP address.
    /// </summary>
    /// <value>State/Province or Political Subdivision abbreviation based on consumer&#39;s public IP address.</value>
    [JsonProperty("state")]
    public string State { get; set; }

    /// <summary>
    /// Timezone name based on consumer&#39;s public IP address.
    /// </summary>
    /// <value>Timezone name based on consumer&#39;s public IP address.</value>
    [JsonProperty("time_zone")]
    public string VarTimeZone { get; set; }
}

public class RetainResult
{
    /// <summary>
    ///         Any text that may help you identify the lead associated with the         certificate such as a unique lead          identifier or URL pointing to the lead in another system. This value         will be displayed in your copy of          the certificate for your future reference. 
    /// </summary>
    /// <value>        Any text that may help you identify the lead associated with the         certificate such as a unique lead          identifier or URL pointing to the lead in another system. This value         will be displayed in your copy of          the certificate for your future reference. </value>
    /*
    <example>1128238382829</example>
    */
    [JsonProperty("reference")]
    public string Reference { get; set; }

    /// <summary>
    /// Gets or Sets Result
    /// </summary>
    [JsonProperty("result")]
    public RetainResultResult Result { get; set; }

    /// <summary>
    ///     When retaining a certificate, you can optionally pass the vendor     name. This is intended for tracking the name     of the company that provided the lead associated with the     certificate. TrustedForm will record this value on the      certificate stored in your account. Other accounts cannot see this     value. When you use TrustedForm reporting,      you can easily filter or group by vendor. 
    /// </summary>
    /// <value>    When retaining a certificate, you can optionally pass the vendor     name. This is intended for tracking the name     of the company that provided the lead associated with the     certificate. TrustedForm will record this value on the      certificate stored in your account. Other accounts cannot see this     value. When you use TrustedForm reporting,      you can easily filter or group by vendor. </value>
    /*
    <example>Acme Co.</example>
    */
    [JsonProperty("vendor")]
    public string Vendor { get; set; }
}

public class RetainResultResult
{
    /// <summary>
    /// The UTC ISO8601 formatted date and time when this certificate will no longer be available for API requests.
    /// </summary>
    /// <value>The UTC ISO8601 formatted date and time when this certificate will no longer be available for API requests.</value>
    /*
    <example>2023-07-18T12:03:52Z</example>
    */
    [JsonProperty("expires_at")]
    public string ExpiresAt { get; set; }

    /// <summary>
    /// A certificate URL that masks the lead source URL and snapshot
    /// </summary>
    /// <value>A certificate URL that masks the lead source URL and snapshot</value>
    [JsonProperty("masked_cert_url")]
    public string MaskedCertUrl { get; set; }

    /// <summary>
    /// A boolean indicating whether your account had already retained this certificate.
    /// </summary>
    /// <value>A boolean indicating whether your account had already retained this certificate.</value>
    /*
    <example>false</example>
    */
    [JsonProperty("previously_retained")]
    public bool? PreviouslyRetained { get; set; }
}

public class MatchLeadResultResult
{
    /// <summary>
    ///     A &#x60;boolean&#x60; indicating if the specified &#x60;email&#x60; was found on the     certificate. A &#x60;null&#x60; value indicates that no emails were     provided. 
    /// </summary>
    /// <value>    A &#x60;boolean&#x60; indicating if the specified &#x60;email&#x60; was found on the     certificate. A &#x60;null&#x60; value indicates that no emails were     provided. </value>
    /*
    <example>true</example>
    */
    [JsonProperty("email_match")]
    public bool? EmailMatch { get; set; }

    /// <summary>
    ///     A &#x60;boolean&#x60; indicating if the specified &#x60;phone&#x60; was found on the     certificate. A &#x60;null&#x60; value indicates that a phone number was     not provided. 
    /// </summary>
    /// <value>    A &#x60;boolean&#x60; indicating if the specified &#x60;phone&#x60; was found on the     certificate. A &#x60;null&#x60; value indicates that a phone number was     not provided. </value>
    /*
    <example>false</example>
    */
    [JsonProperty("phone_match")]
    public bool? PhoneMatch { get; set; }

    /// <summary>
    ///     A &#x60;boolean&#x60; indicating if any matches were found during the lead     matching operation. A &#x60;null&#x60; value indicates that lead matching     was not performed. 
    /// </summary>
    /// <value>    A &#x60;boolean&#x60; indicating if any matches were found during the lead     matching operation. A &#x60;null&#x60; value indicates that lead matching     was not performed. </value>
    /*
    <example>true</example>
    */
    [JsonProperty("success")]
    public bool? Success { get; set; }
}

/// <summary>
///   The result of the &#x60;match_lead&#x60; operation. The &#x60;email&#x60; and &#x60;phone&#x60; parameters are echoed and the   &#x60;result&#x60; property reports the outcome of the operation. More information is [available in our KB](https://community.activeprospect.com/posts/4766190-trustedform-lead-matching).    If the operation result is not a &#x60;success&#x60;, TrustedForm was unable to confirm that the consumer information   collected on the cert matches the lead it came with. This is a strong signal that the lead should not be contacted.    The result of the &#x60;match_lead&#x60; operation does not impact the behavior of the &#x60;retain&#x60; operation. 
/// </summary>
public class MatchLeadResult
{
    /// <summary>
    /// The email or hashed value provided in the request, believed to be that of the consumer recorded in the certificate.
    /// </summary>
    /// <value>The email or hashed value provided in the request, believed to be that of the consumer recorded in the certificate.</value>
    [JsonProperty("email")]
    public string Email { get; set; }

    /// <summary>
    /// The phone number or hashed value provided in the request, believed to be that of the consumer recorded in the certificate.
    /// </summary>
    /// <value>The phone number or hashed value provided in the request, believed to be that of the consumer recorded in the certificate.</value>
    [JsonProperty("phone")]
    public string Phone { get; set; }

    /// <summary>
    /// Gets or Sets Result
    /// </summary>
    [JsonProperty("result")]
    public MatchLeadResultResult Result { get; set; }
}