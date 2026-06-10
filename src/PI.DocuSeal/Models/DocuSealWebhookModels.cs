namespace PI.DocuSeal.Models;

public class DocuSealWebhookConfiguration
{
    public const string SectionName = "DocuSealWebhook";
    
    public string WebhookSecret { get; set; } = string.Empty;
    public bool RequireSignatureVerification { get; set; } = false;
    public List<string> AllowedEventTypes { get; set; } = new()
    {
        "submission.created",
        "submission.completed",
        "submitter.completed",
        "submitter.opened"
    };
}