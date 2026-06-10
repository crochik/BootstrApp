namespace Models;

public static class VerseEventType
{
    //	Triggered for all activity related to your lead (Lead Created, Notes, Messages, Calls, Emails, and (Un)Qualification).
    public const string Activity = "lead_activity";

    // triggered when a lead is created. This is useful to be notified when you receive inbound calls.
    public const string LeadCreated = "lead_created";

    // Triggered when a lead is qualified. This will include all the customQuestions answered
    public const string LeadQualified = "lead_qualify";

    // Triggered when a lead is unqualified. This will include all the Custom Questions answered and the reasonUnqualified    
    public const string LeadUnqualified = "lead_unqualify";
}
