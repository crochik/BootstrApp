using System.Collections.Generic;

namespace Models;

public static class VerseTitle
{
    public const string CallForwarded = "Call Forwarded";
    public const string ConciergeNote = "Concierge Note";
    
    public const string InboundCallReceived = "Inbound Call Received";
    public const string InboundEmail = "Inbound Email";
    public const string InboundSMS = "Inbound SMS";
    
    public const string LeadCreated = "Lead Created";
    
    public const string LiveTransferAttempt = "Live Transfer Attempt";
    public const string LiveTransferSuccessful = "Live Transfer Successful";
    public const string LiveTransferUnsuccessful = "Live Transfer Unsuccessful";

    public const string OutboundCallAttempt = "Outbound Call Attempt";
    public const string OutboundEmail = "Outbound Email";
    public const string OutboundSMS = "Outbound SMS";
    
    public const string QualifiedLead = "Qualified Lead";
    public const string UnqualifiedLead = "Unqualified Lead";
    public const string VerseActivityLog = "Verse Activity Log";

    public static readonly Dictionary<string, string> TitleDescription = new Dictionary<string, string>
    {
        {LeadCreated, "This is sent when subscribed to the events lead_created or Lead Activity and a new lead was created."},
        {QualifiedLead, "This is sent when subscribed to the events lead_activity or lead_qualify and the lead was qualified."},
        {UnqualifiedLead, "This is sent when subscribed to the events lead_activity or lead_unqualify and the lead was unqualified."},
        {InboundSMS, "This is sent when subscribed to the event lead_activity and an inbound SMS was received from the lead."},
        {OutboundSMS, "This is sent when subscribed to the event lead_activity and an outbound SMS was sent to the lead."},
        {ConciergeNote, "This is sent when subscribed to the event lead_activity and the Concierge created a new note for the lead. This typically occurs after a lead is qualified or unqualified and additional actions from your team has been requested."},
        {OutboundEmail, "This is sent when subscribed to the event lead_activity and an outbound email to the Lead has been sent."},
        {InboundEmail, "This is sent when subscribed to the event lead_activity and an inbound email from the Lead has been recieved."},
        {OutboundCallAttempt, "This is sent when subscribed to the event lead_activity and an outbound call attempt to the lead has been attempted."},
        {InboundCallReceived, "This is sent when subscribed to the event lead_activity and an inbound call from the lead has been received."},
        {LiveTransferAttempt, "This is sent when subscribed to the event lead_activity and the lead was attempted to be live transferred to your team."},
        {LiveTransferSuccessful, "This is sent when subscribed to the event lead_activity and the lead was successfully live transferred to your team."},
        {LiveTransferUnsuccessful, "This is sent when subscribed to the event lead_activity and the lead was unsuccessfully live transferred to your team."},
        {CallForwarded, "This is sent when subscribed to the event lead_activity and the lead was forwarded to your team (this only occurs if you're using Call-Connect"},
        {VerseActivityLog, "This is sent when subscribed to the event lead_activity and the script has progressed and the lead's response has been captured and logged."},
    };
}