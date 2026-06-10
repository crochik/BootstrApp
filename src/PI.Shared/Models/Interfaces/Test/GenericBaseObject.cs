using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.Interfaces.Test;

public class GenericBaseObject : IBase
{
    /// <summary>
    /// Should be the type (_t?)
    /// and just have ObjectType be an alias? 
    /// </summary>
    [BsonIgnore]
    public string ObjectType => GetType().Name;

    public Guid Id { get; }
    public Guid AccountId { get; }
    public string Name { get; }
    public Guid EntityId { get; }
    public string Description { get; set; }
    public Guid? ObjectStatusId { get; }
    public Guid? FlowId { get; }
    public bool IsActive { get; }
    public DateTime CreatedOn { get; }
    public DateTime? LastModifiedOn { get; }
    public Actor LastActor { get; }
}

public class GenericFile : GenericBaseObject, IFile
{
    public ReferencedObject Parent { get; set; }
}

public class GenericNote : GenericBaseObject, INote
{
    public string ContentType { get; set; }
    public string Content { get; set; }
    public ReferencedObject Parent { get; set; }
    public Dictionary<string, object> RelatedObjects { get; set; }
    public Guid? CreatorId { get; set; }
}

public class GenericTask : GenericNote, ITask
{
    public Guid AssignedUserId { get; set; }
    public DateTime? DueDate { get; set; }
    public string Priority { get; set; }
    public AddressComponents Address { get; set; }
}

public class GenericAppointment : GenericNote, IAppointment
{
    public bool IsAllDay { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string TimeZoneId { get; set; }
    public AddressComponents Address { get; set; }
}

public class TouchPoint : GenericNote, ITouchPoint
{
    
}

public class KeepInTouchRule : GenericBaseObject
{
    public Criteria Criteria { get; set; }
    public int Priority { get; set; }
}

public class KeepInTouchPreferences : GenericBaseObject
{
    
}