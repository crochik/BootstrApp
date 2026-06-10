using System;
using System.Collections.Generic;
using Crochik.Mongo;

namespace PI.Shared.Models.Notifications;

[BsonCollection("notification.Notification")]
public class Notification : FlowObjectModel
{
    // Name = Title 
    // Description = Message 
    
    /// <summary>
    /// notification category so users can opt in/out 
    /// </summary>
    public string Category { get; set; }
    
    /// <summary>
    /// Url to launch (in new tab)
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Action (Url) to launch within the app
    /// </summary>
    public string Action { get; set; }
    
    /// <summary>
    /// Client Id, if not default
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// When this message will expire (become meaningless) 
    /// </summary>
    public DateTime? ExpiresOn { get; set; }
    
    /// <summary>
    /// When was it read the first time
    /// </summary>
    public DateTime? ReadOn { get; set; }
    
    public List<KeyValuePair<string, object>> Refs { get; set; }
 
    /// <summary>
    /// List of subscribers at the time the notification was created
    /// </summary>
    public NotificationSubscriber[] Subscribers { get; set; }
}