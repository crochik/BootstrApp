using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models;

[JsonConverter(typeof(StringEnumConverter))] 
public enum ExternalProvider
{
    Unknown,
    Microsoft,
    InspireNet,
    Zoom,
    SendGrid,
    GoToMeeting,
    Okta,
    Google,
    RealMagic,
    Salesforce,
    Stripe,
    Bootstrapp, 
    CompanyCam,
    Quickbooks,
    GitHub
};