using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using PI.Shared.Models.Client;

namespace PI.Shared.Models
{
    /// <summary>
    /// Defines entity/model mapping for identity resources.
    /// </summary>
    /// <seealso cref="AutoMapper.Profile" />
    public class IdentityResourceMapperProfile : Profile
    {
        /// <summary>
        /// <see cref="IdentityResourceMapperProfile"/>
        /// </summary>
        public IdentityResourceMapperProfile()
        {
            CreateMap<List<UserClaim>, ICollection<string>>()
                .ConstructUsing(s => s.Select(x => x.Type).ToList());

            CreateMap<IdentityResource, IdentityServer4.Models.IdentityResource>(MemberList.Destination)
                .ConstructUsing(src => new IdentityServer4.Models.IdentityResource(src.Name, src.DisplayName, src.UserClaims.Select(x => x.Type)))
                // .ForMember(d => d.UserClaims, o => o.Ignore())
                // .ForMember(d=>d.Properties, o=>o.Ignore())
                // .ReverseMap();
                ;
        }
    }
}