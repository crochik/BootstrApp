using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Form.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum TriggerType
{
    SideEffect,
    System,
    User,
    Error,
    Scheduled,
}

[DiscriminatorWithFallback]
[BsonDiscriminator(Required = true)]
[BsonKnownTypes(
    typeof(SystemTrigger),
    typeof(UserTrigger),
    typeof(ScheduledTrigger),
    typeof(ErrorTrigger)
)]
public class Trigger
{
    [BsonIgnore]
    public virtual TriggerType Type => TriggerType.SideEffect;
        
    public string Name { get; set; }
        
    /// <summary>
    /// (optional) Action that generated this event
    /// </summary>
    public Guid? ActionId { get; set; }
        
    /// <summary>
    /// (optional) Object status where the action generated this event
    /// OR... for what the trigger applies (user trigger, delayed trigger)
    /// </summary>
    public Guid? ObjectStatusId { get; set; }
}
    
[BsonDiscriminator("System")]
public class SystemTrigger : Trigger
{
    public override TriggerType Type => TriggerType.System;
}

[BsonDiscriminator("User")]
public class UserTrigger : Trigger
{
    public override TriggerType Type => TriggerType.User;

    /// <summary>
    /// Form
    /// </summary>
    public Form.Models.Form Form { get; set; }

    /// <summary>
    /// Whether this action should be an option when MULTIPLE items are selected
    /// </summary>
    public bool AllowMultiple { get; set; }

    /// <summary>
    /// Whether this action should be an option when NO items are selected
    /// </summary>
    public bool AllowNone { get; set; }

    /// <summary>
    /// (optional) Use this version for these profiles
    /// </summary>
    public Guid[] ProfileIds { get; set; }

    /// <summary>
    /// (optional) Role 
    /// </summary>
    public EntityRoleId? Role { get; set; }        
        
    /// <summary>
    /// (optional) message to display to the user after the event is fired
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// (optional) action to execute after the event is fired
    /// </summary>
    public string NextUrl { get; set; }        
        
    /// <summary>
    /// (optional) User action is hidden (will only be triggered by another action)
    /// </summary>
    public bool? IsHidden { get; set; }
        
    /// <summary>
    /// (optional) Object Type to be used when taking a snapshot
    /// </summary>
    public string SnapshotObjectType { get; set; }
    
    /// <summary>
    /// (optional) list of related objects to read
    /// </summary>
    public string[] RelatedObjects { get; set; }
    
    public string Icon { get; set; }
    
    /// <summary>
    /// Conditions for the action (based on object properties and/or Context)
    /// - should be processed in the API. Will be excluded if any of the conditions returns false
    /// - should be enforced in the API when the action is triggered 
    /// </summary>
    public Condition[] Conditions { get; set; }
        
    /// <summary>
    /// (optional) When set, this trigger is only eligible after the min
    /// NOT IMPLEMENTED YET
    /// </summary>
    public TimeSpan? MinSinceStatusChange { get; set; }

    /// <summary>
    /// (optional) When set, this trigger is only eligible before the max
    /// NOT IMPLEMENTED YET
    /// </summary>
    public TimeSpan? MaxSinceStatusChange { get; set; }
    
    /// <summary>
    /// used to sort actions in the menu
    /// NOT IMPLEMENTED YET 
    /// - priority 0, could indicate being the default
    /// - priority 0: could automatically be associated with clicking on the item in the grid  
    /// </summary>
    public int? Priority { get; set; }
    
    public string InputObjectType { get; set; }
    public string OutputObjectType { get; set; }
}

[BsonDiscriminator("Scheduled")]
public class ScheduledTrigger : Trigger
{
    public override TriggerType Type => TriggerType.Scheduled;
    
    public DateTime Start { get; set; }
    public Condition[] Criteria { get; set; }
    public string Schedule { get; set; }
    
    /// <summary>
    /// when set, it is specific to one flow... most likely scenario 
    /// </summary>
    public Guid? FlowId { get; set; }

    /// <summary>
    /// whether it is active 
    /// </summary>
    public bool IsActive { get; set; } = true;
}

[BsonDiscriminator("Error")]
public class ErrorTrigger : Trigger
{
    public override TriggerType Type => TriggerType.Error;
}