using System;
using Crochik.Mongo;

namespace PI.Shared.Models.Notifications;

/// <summary>
/// Subscription to a category of notifications
/// EntityId is the user subscribed to 
/// </summary>
[BsonCollection("notification.Subscriber")]
public class NotificationSubscriber : EntityOwnedModel
{
    /// <summary>
    /// Notification Category (externalId) to subscribe to 
    /// </summary>
    public string Category { get; set; }   
    
    /// <summary>
    /// (Optional) Notification target Entity Id to subscribe to
    /// When missing, subscription to any notification in the category
    /// </summary>
    public Guid? DestinationEntityId { get; set; }
    
    /// <summary>
    /// Communication channel to receive notifications (sms, phone, email, ...)
    /// </summary>
    public string CommunicationChannel { get; set; }
    
    /// <summary>
    /// Optional channel address (phone number, email address, ... )
    /// </summary>
    public string ChannelAddress { get; set; }
}