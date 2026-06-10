using AutoMapper;

namespace SchedulerAPI.Profiles
{
    public class LeadProfile : Profile
    {
        public LeadProfile()
        {
            CreateMap<PI.Shared.Models.Lead, Controllers.Models.Lead>(MemberList.Destination);
        }
    }
}