using System;
using System.Dynamic;

namespace Messages.Flow;

public class FireWebhookActionOptions : ActionOptions
{
    public string EventId { get; set; }
    public string EventName { get; set; }
    public string EventDescription { get; set; }
    
    /// <summary>
    /// Expression to resolve Organization Id that should receive this notification
    /// can resolve to null or be null
    /// </summary>
    public string OrganizationId { get; set; }
    
    /// <summary>
    /// Extra properties to be added to payload
    /// </summary>
    public ExpandoObject ExtraProperties { get; set; }
}