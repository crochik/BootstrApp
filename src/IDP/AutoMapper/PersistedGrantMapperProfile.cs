using AutoMapper;
using PI.Shared.Models.Client;

namespace PI.Shared.Models
{
    /// <summary>
    /// Defines entity/model mapping for persisted grants.
    /// </summary>
    /// <seealso cref="AutoMapper.Profile" />
    public class PersistedGrantMapperProfile : Profile
    {
        /// <summary>
        /// <see cref="PersistedGrantMapperProfile">
        /// </see>
        /// </summary>
        public PersistedGrantMapperProfile()
        {
            CreateMap<PersistedGrant, IdentityServer4.Models.PersistedGrant>(MemberList.Destination)
                .ReverseMap();
        }
    }
}