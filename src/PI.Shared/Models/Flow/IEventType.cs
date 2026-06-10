using System.Collections.Generic;
using System.Dynamic;
using AutoMapper;
using Messages.Flow;

namespace PI.Shared.Models;

public interface IEventType : IEntityOwnedModel
{
    /// <summary>
    /// Trigger for this event
    /// </summary>
    Trigger Trigger { get; }

    /// <summary>
    /// Object type that this event applies to
    /// </summary>
    string ObjectType { get; }
}

public class EventType : EntityOwnedModel, IEventType
{
    private Trigger _trigger;

    public Trigger Trigger
    {
        get => _trigger ??= new Trigger();
        set => _trigger = value;
    }

    public string ObjectType { get; set; }
    
    /// <summary>
    /// Summary for/from AI 
    /// </summary>
    public string Summary { get; set; }
}

public class EventTypeProfile : Profile
{
    public EventTypeProfile()
    {
        CreateMap<IEventType, EventType>(MemberList.Source);
    }
}

public class RunStep
{
    public FlowEvent Event { get; set; }
    public FlowStep[] Steps { get; set; }
}

public class ObjectWithType
{
    public string ObjectType { get; init; }
    public IDictionary<string, object> Object { get; init; }
}