using System;

namespace Messages.Flow;

public class SendNotificationActionOptions : ActionOptions
{
    /// <summary>
    /// Title "expression"  
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Message "handlebars template"
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Url  (expression) 
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Action (Local Url) "expression" 
    /// </summary>
    public string Action { get; set; }

    /// <summary>
    /// Category or Property Path 
    /// </summary>
    public string Category { get; set; }
    
    /// <summary>
    /// Entity to be notified (expression)
    /// </summary>
    public string EntityId { get; set; }
    
    /// <summary>
    /// Event after push notification is sent  
    /// </summary>
    public Guid? PushNotificationEventId { get; set; }

    /// <summary>
    /// Event for email subscribers  
    /// </summary>
    public Guid? EmailNotificationEventId { get; set; }
    
    /// <summary>
    /// Event for SMS subscribers  
    /// </summary>
    public Guid? SMSNotificationEventId { get; set; }
    
    /// <summary>
    /// Fire when no subscriptions for the notification are found
    /// </summary>
    public Guid? NoSubscriptionsEventId { get; set; }
    
    /// <summary>
    /// Client Id used to customize what (firebase) service to use
    /// </summary>
    public string ClientId { get; set; }
    
    // /// <summary>
    // /// TTL before the notification becomes meaningless 
    // /// </summary>
    // public int? TTL { get; set; }
    //
    // /// <summary>
    // /// Flow for the new notification
    // /// </summary>
    // public Guid FlowId { get; set; }
    //
    // /// <summary>
    // /// Initial status for new notification
    // /// </summary>
    // public Guid? ObjectStatusId { get; set; }
    
    public override ActionOutput[] Output { get; set; }
}