using System;
using System.Collections.Generic;
using Crochik.Mongo;
using PI.Shared.Models.Interfaces;

namespace PI.Shared.Models;

[BsonCollection("Task")]
public class ObjectTask : EntityOwnedModel, ITask
{
    public string ObjectType => "Task";
    
    public Guid? ObjectStatusId { get; set; }
    public Guid? FlowId { get; set; }
    public bool IsActive { get; set;  }
    public string ContentType { get; set; }
    public string Content { get; set; }
    public ReferencedObject Parent { get; set; }
    public Dictionary<string, object> RelatedObjects { get; set; }
    public Guid? CreatorId { get; set; }
    public AddressComponents Address { get; set; }
    public Guid AssignedUserId { get; set; }
    public DateTime? DueDate { get; set; }
    
    // use to be numbers
    public string Priority { get; set; }
}