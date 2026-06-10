using System;
using System.Collections.Generic;
using System.Dynamic;
using PI.Shared.Models;

namespace PI.Shared.Services;

public class ActionRunState
{
    public IEntityContext Context { get; init; }
    public User User { get; init; }
    public ExpandoObject ExpandoObject { get; init; }
    public string Description { get; init; }
    public ObjectType ObjectType { get; init; }
    public EventType EventType { get; init; }
    
    public Guid FlowId { get; set; }
    public Guid? ObjectStatusId { get; set; }
    public Guid ObjectId { get; set; }

    public Flow Flow { get; set; }

    public FlowRun FlowRun    {
        get;
        set
        {
            field = value;
            ObjectFlowRunId = field?.Id ?? Guid.NewGuid();
        }
    }
    
    public UserTrigger Trigger => EventType?.Trigger as UserTrigger;
    public Guid EventTypeId => EventType.Id;

    public Guid ObjectFlowRunId { get; private set; } = Guid.NewGuid();
    public Dictionary<string, object> FlatObject { get; set; }
}