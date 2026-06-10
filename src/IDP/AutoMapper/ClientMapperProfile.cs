using System.Linq;
using System.Security.Claims;
using AutoMapper;
using PI.Shared.Models.Client;

namespace PI.Shared.Models;

/// <summary>
/// Defines entity/model mapping for clients.
/// </summary>
/// <seealso cref="AutoMapper.Profile" />
public class ClientMapperProfile : Profile
{
    /// <summary>
    /// <see>
    ///     <cref>{ClientMapperProfile}</cref>
    /// </see>
    /// </summary>
    public ClientMapperProfile()
    {
        CreateMap<AppClient, IdentityServer4.Models.Client>()
            .ForMember(dest => dest.ProtocolType, opt => opt.Condition(srs => srs != null))
            // .ForMember(x=>x.AllowedCorsOrigins, o=>o.MapFrom
            //     (
            //     s=>s.AllowedCorsOrigins
            //         .Select(x=>x.Origin)
            //         .Where(x=>x.StartsWith("http"))
            //         .ToArray()
            //     )
            // )
            .ForMember(d => d.AllowedCorsOrigins, o => o.Ignore()) // not used since the IdentityServer will defer to the CorsPolicyServer to make a decision
            .ForMember(d => d.ClientSecrets, o => o.MapFrom(s => s.ClientSecrets))
            // .ReverseMap();
            ;

        CreateMap<ClientCorsOrigin, string>()
            .ConstructUsing(src => src.Origin)
            // .ReverseMap()
            // .ForMember(dest => dest.Origin, opt => opt.MapFrom(src => src));
            ;

        CreateMap<ClientIdPRestriction, string>()
            .ConstructUsing(src => src.Provider)
            // .ReverseMap()
            // .ForMember(dest => dest.Provider, opt => opt.MapFrom(src => src));
            ;

        CreateMap<ClientClaim, Claim>(MemberList.Destination)
            .ConstructUsing(src => new Claim(src.Type, src.Value))
            .ForMember(d => d.Properties, o => o.Ignore())
            // .ReverseMap();
            ;

        CreateMap<ClientClaim, IdentityServer4.Models.ClientClaim>()
            // .ReverseMap();
            ;

        CreateMap<ClientScope, string>()
            .ConstructUsing(src => src.Scope)
            // .ReverseMap()
            // .ForMember(dest => dest.Scope, opt => opt.MapFrom(src => src));
            ;

        CreateMap<ClientPostLogoutRedirectUri, string>()
            .ConstructUsing(src => src.PostLogoutRedirectUri)
            // .ReverseMap()
            // .ForMember(dest => dest.PostLogoutRedirectUri, opt => opt.MapFrom(src => src));
            ;

        CreateMap<ClientRedirectUri, string>()
            .ConstructUsing(src => src.RedirectUri)
            // .ReverseMap()
            // .ForMember(dest => dest.RedirectUri, opt => opt.MapFrom(src => src));
            ;

        CreateMap<ClientGrantType, string>()
            .ConstructUsing(src => src.GrantType)
            // .ReverseMap()
            // .ForMember(dest => dest.GrantType, opt => opt.MapFrom(src => src));
            ;
    }
}