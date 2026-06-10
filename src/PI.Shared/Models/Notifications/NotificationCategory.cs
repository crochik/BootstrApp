using Crochik.Mongo;

namespace PI.Shared.Models.Notifications;

[BsonCollection("notification.Category")]
public class NotificationCategory : EntityOwnedModel,IExternalId
{
    /// <summary>
    /// Unique identifier for the category
    /// </summary>
    public string ExternalId { get; set;  }
    
    /// <summary>
    /// for what role this notification apply (e.g. User, Organization, ...)
    /// </summary>
    public EntityRoleId EntityRole { get; set; }
}