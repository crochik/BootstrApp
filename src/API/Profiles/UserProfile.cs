using System;
using System.Linq;
using AutoMapper;
using PI.Shared.Models;

namespace SchedulerAPI.Profiles
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<PI.Shared.Models.User, Controllers.Models.User>()
                .ForMember(user => user.Role, config => config.MapFrom(src => Enum.Parse(typeof(EntityRoleId), src.UserRoleId)))
                .ForMember(d => d.Identities, o => o.MapFrom((s, d, member, context) => s.GetIdentities()?.Select(x => context.Mapper.Map<Controllers.Models.Identity>(x)).ToArray()))
                ;
        }
    }

}
