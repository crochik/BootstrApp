using System;
using AutoMapper;
using PI.Shared.Models;

namespace Controllers.Models
{
    public class ApiModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class ApiEntityOwnedModel : ApiModel
    {
        public Guid AccountId { get; set; }
        public string Description { get; set; }
        public Guid EntityId { get; set; }
    }

    public class LeadStatus : ApiEntityOwnedModel, ILeadStatus
    {
    }

    public class EventType : ApiEntityOwnedModel, IEventType
    {
        public Trigger Trigger { get; set; }

        // public string Type { get; set; }
        public string ObjectType { get; set; }
    }

    public class Flow : ApiEntityOwnedModel, IFlow
    {
        // public Guid[] EmbeddedFlowIds { get; set; }
        public string ObjectType { get; set; }

        public PI.Shared.Models.FlowStep[] GetSteps() => null;
    }

    public class FlowAction : ApiEntityOwnedModel, IFlowAction
    {
    }

    public class ApiNewModelProfile : Profile
    {
        public ApiNewModelProfile()
        {
            CreateMap<PI.Shared.Models.EntityOwnedModel, ApiEntityOwnedModel>();

            CreateMap<PI.Shared.Models.ObjectStatus, LeadStatus>(MemberList.Destination);
            CreateMap<PI.Shared.Models.EventType, EventType>();
            CreateMap<PI.Shared.Models.Flow, Flow>();

            CreateMap<FlowActions.IFlowActionBuilder, FlowAction>()
                .ForMember(d => d.AccountId, o => o.Ignore())
                .ForMember(d => d.EntityId, o => o.Ignore())
                ;
        }
    }
}