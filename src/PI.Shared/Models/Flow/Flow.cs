using AutoMapper;

namespace PI.Shared.Models;

public class Flow : EntityOwnedModel, IFlow
{
    public FlowStep[] Steps { get; set; }
        
    // [Obsolete("never used?")]
    // public FlowTransition[] Transitions { get; set; }
    // public Guid[] EmbeddedFlowIds { get; set; }
        
    public string ObjectType { get; set; }
    public FlowStep[] GetSteps() => Steps;
}

public class FlowProfile : Profile
{
    public FlowProfile()
    {
        CreateMap<IFlow, Flow>(MemberList.Source)
            .ForMember(x => x.Steps, o => o.Ignore())
            // .ForMember(x => x.Transitions, o => o.Ignore())
            ;

        CreateMap<FlowStep, FlowStep>(MemberList.Source)
            // .ForMember(x => x.Options, o => o.MapFrom(s => s.Options))
            // .ReverseMap()
            ;
    }
}

public class Placeholder
{
    public enum PlaceholderType
    {
        Value,
        Object,
    }

    public PlaceholderType Type { get; set; } 
    public string ObjectType { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}