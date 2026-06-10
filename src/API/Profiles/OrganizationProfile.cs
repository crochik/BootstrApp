using System.Linq;
using AutoMapper;

namespace SchedulerAPI.Profiles
{
    public class OrganizationProfile : Profile
    {
        public OrganizationProfile()
        {
            CreateMap<PI.Shared.Models.Organization, Controllers.Models.Organization>()
                .ForMember(d => d.Identities, o => o.MapFrom((s, d, member, context) => s.GetIdentities()?.Select(x => context.Mapper.Map<Controllers.Models.Identity>(x)).ToArray()));
        }
    }

}
