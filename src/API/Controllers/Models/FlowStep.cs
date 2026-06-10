using System;
using AutoMapper;
using FlowActions;
using Messages.Flow;

namespace Controllers.Models
{
    public class FlowStepUpdate 
    {
        public Guid EventIdTrigger { get; set; }
        public Guid? CurrentStatusId { get; set; }
        public Guid ActionId { get; set; }
        public dynamic Options { get; set; }
        public string Description { get; set; }
        public string IconName { get; set; }
    }

    public class FlowStep // : PI.Shared.Models.IFlowStep
    {
        public Guid Id { get; set; }
        public Guid EventIdTrigger { get; set; }
        public Guid? CurrentStatusId { get; set; }
        public Guid ActionId { get; set; }
        public IActionOptions Options { get; set; }
        public string Description { get; set; }
        public string IconName { get; set; }
    }

    public class FlowStepProfile : Profile
    {
        public FlowStepProfile()
        {
            CreateMap<PI.Shared.Models.FlowStep, Controllers.Models.FlowStep>();            

            CreateMap<FlowStepUpdate, ParseContext>(MemberList.Source);
        }
    }
}