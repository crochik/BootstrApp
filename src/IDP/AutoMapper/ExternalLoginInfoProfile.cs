using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace PI.Shared.Models
{
    public class ExternalLoginInfoProfile : Profile
    {
        public ExternalLoginInfoProfile()
        {
            // replaced with inline functions as these are "one time"/odd ducks
            
            // CreateMap<IEnumerable<AuthenticationToken>, Token>(MemberList.None)
            //     .ConstructUsing((list, context) =>
            //     {
            //         var token = new Token();
            //         foreach (var pair in list)
            //         {
            //             switch (pair.Name)
            //             {
            //                 case "access_token": token.AccessToken = pair.Value; break;
            //                 case "refresh_token": token.RefreshToken = pair.Value; break;
            //                 case "expires_at": token.Expiration = DateTime.Parse(pair.Value); break;
            //                 // case "token_type": // === Bearer
            //                 // default:
            //                 //     break;
            //             }
            //         }
            //
            //         return token;
            //     });
            
            // CreateMap<System.Security.Principal.IIdentity, Dictionary<string, string>>(MemberList.None)
            //     .ConstructUsing((identity, context) =>
            //     {
            //         var claims = new Dictionary<string, string>();
            //         claims.Add("AuthenticationType", identity.AuthenticationType);
            //         claims.Add("Name", identity.Name);
            //
            //         if (identity is System.Security.Claims.ClaimsIdentity claimsIdentity)
            //         {
            //             foreach (var claim in claimsIdentity.Claims)
            //             {
            //                 claims.Add(claim.Type, claim.Value);
            //             }
            //         }
            //
            //         return claims;
            //     });

            CreateMap<ExternalLoginInfo, ExternalIdentity>(MemberList.None)
                .ConstructUsing((eli, context) =>
                {
                    var identity = new ExternalIdentity
                    {
                        Provider = eli.LoginProvider,
                        ExternalId = eli.ProviderKey,
                        Token = buildToken(),
                        Claims = buildClaims()
                    };

                    return identity;

                    Token buildToken()
                    {
                        if (eli.AuthenticationTokens == null) return null;
                        
                        var token = new Token();
                        foreach (var pair in eli.AuthenticationTokens)
                        {
                            switch (pair.Name)
                            {
                                case "access_token": token.AccessToken = pair.Value; break;
                                case "refresh_token": token.RefreshToken = pair.Value; break;
                                case "expires_at": token.Expiration = DateTime.Parse(pair.Value); break;
                                // case "token_type": // === Bearer
                                // default:
                                //     break;
                            }
                        }

                        return token;                        
                    }

                    Dictionary<string, string> buildClaims()
                    {
                        var input = eli.Principal.Identity;
                        var claims = new Dictionary<string, string>();
                        if (input == null) return claims;

                        claims.Add("AuthenticationType", input.AuthenticationType);
                        claims.Add("Name", input.Name);

                        if (input is System.Security.Claims.ClaimsIdentity claimsIdentity)
                        {
                            foreach (var claim in claimsIdentity.Claims)
                            {
                                if (!claims.TryAdd(claim.Type, claim.Value))
                                {
                                    // hack just to prevent crash
                                    claims[claim.Type] = $"{claims[claim.Type]}, {claim.Value}";;
                                }
                            }
                        }
                        
                        return claims;
                    }
                });

            CreateMap<ExternalIdentity, UserLoginInfo>(MemberList.None)
                .ConstructUsing(src => new UserLoginInfo(src.Provider, src.ExternalId, src.Provider));

            CreateMap<PI.Shared.Models.EntityIdentity, UserLoginInfo>(MemberList.None)
                .ConstructUsing(src => new UserLoginInfo(src.IdentityProviderId, src.ExternalId, src.IdentityProviderId));

            CreateMap<ExternalIdentity, PI.Shared.Models.EntityIdentity>(MemberList.None)
                .ConstructUsing((Func<ExternalIdentity, ResolutionContext, PI.Shared.Models.EntityIdentity>)((src, context) =>
                {
                    var dst = new PI.Shared.Models.EntityIdentity
                    {
                        // EntityId
                        Id = Guid.NewGuid(),
                        IdentityProviderId = src.Provider,
                        ExternalId = src.ExternalId,
                        ExternalIdentity = src // JsonConvert.SerializeObject(src),
                    };

                    return (PI.Shared.Models.EntityIdentity)dst;
                }));

            CreateMap<PI.Shared.Models.EntityIdentity, ExternalIdentity>(MemberList.None)
                .ConstructUsing((System.Linq.Expressions.Expression<Func<PI.Shared.Models.EntityIdentity, ExternalIdentity>>)(src => (ExternalIdentity)src.ExternalIdentity)); // .DeserializeObject<ExternalIdentity>(src.Value));
        }
    }
}