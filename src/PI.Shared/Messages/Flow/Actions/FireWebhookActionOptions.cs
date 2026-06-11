namespace Messages.Flow;

public class FireWebhookActionOptions : ActionOptions
{
    public string EventId { get; set; }
    public string EventName { get; set; }
    public string EventDescription { get; set; }
}