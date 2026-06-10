namespace Zapier.Models;

public class SubscriptionRequest
{
    public string HookUrl { get; set; }
    public string ObjectType { get; set; }
    public string Event { get; set; }
    public string[] Events { get; set; }
}