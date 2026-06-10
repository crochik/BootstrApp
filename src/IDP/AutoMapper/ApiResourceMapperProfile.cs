using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using PI.Shared.Models.Client;

namespace PI.Shared.Models;

/// <summary>
/// Defines entity/model mapping for API resources.
/// </summary>
/// <seealso cref="AutoMapper.Profile" />
public class ApiResourceMapperProfile : Profile
{
    /// <summary>
    /// <see cref="ApiResourceMapperProfile"/>
    /// </summary>
    public ApiResourceMapperProfile()
    {
        CreateMap<Property, KeyValuePair<string, string>>()
            // .ReverseMap();
            ;

        CreateMap<ApiResource, IdentityServer4.Models.ApiResource>(MemberList.Destination)
            .ConstructUsing(src => new IdentityServer4.Models.ApiResource())
            .ForMember(x => x.ApiSecrets, opts => opts.MapFrom(x => x.Secrets))
            .ForMember(x => x.Scopes, opts => opts.MapFrom(s => s.Scopes.Select(x => x.Name)))
            // .ReverseMap()
            ;

        CreateMap<UserClaim, string>()
            .ConstructUsing(x => x.Type)
            // .ReverseMap()
            // .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src));
            ;

        CreateMap<Secret, IdentityServer4.Models.Secret>()
            .ForMember(dest => dest.Type, opt => opt.Condition(srs => srs != null))
            // .ReverseMap()
            ;

        CreateMap<ApiScope, IdentityServer4.Models.ApiScope>(MemberList.Destination)
            .ConstructUsing(src => new IdentityServer4.Models.ApiScope(src.Name, src.DisplayName, src.UserClaims.Select(x => x.Type)))
            // .ReverseMap();
            ;
    }
}