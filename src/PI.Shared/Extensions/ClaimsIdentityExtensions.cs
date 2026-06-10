using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using IdentityModel;
using Crochik.Extensions;

namespace PI.Shared.Models;

public static class ClaimsIdentityExtensions
{
    public static IContextWithActor GetEntityContextWithActor(this ClaimsIdentity identity, string requestId = null)
        =>
            identity.Claims.GetEntityContextWithActor(requestId);

    public static IContextWithActor GetEntityContextWithActor(this ClaimsPrincipal identity, string requestId = null)
        =>
            identity.Claims.GetEntityContextWithActor(requestId);

    private static IContextWithActor GetEntityContextWithActor(this IEnumerable<Claim> claims, string requestId = null)
    {
        var userId = default(Guid?);
        var organizationId = default(Guid?);
        var accountId = default(Guid?);
        var name = default(string);
        var entityRole = default(EntityRoleId?);
        var clientId = default(string);
        var tokenId = default(string);
        var profileId = default(Guid?);
        IEnumerable<string> scope = null;
        IEnumerable<Guid> additionalProfileIds = null;

        var contextClaims = new Dictionary<string, string[]>();
        foreach (var claim in claims)
        {
            if (contextClaims.TryGetValue(claim.Type, out var prevValues))
            {
                contextClaims[claim.Type] = prevValues.Append(claim.Value).ToArray();
            }
            else
            {
                contextClaims[claim.Type] = [claim.Value];
            }

            switch (claim.Type)
            {
                case JwtClaimTypes.Subject:
                    userId = Guid.Parse(claim.Value);
                    break;

                case JwtClaimTypes.Name:
                case "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name":
                    if (name == null) name = claim.Value;
                    break;

                case JwtClaimTypes.PreferredUserName:
                    name = claim.Value;
                    break;

                case JwtClaimTypes.ClientId:
                    clientId = claim.Value;
                    break;

                case "client_profile_id":
                case "pi_profile_id":
                    profileId = Guid.Parse(claim.Value);
                    break;

                case "pi_profile_ids":
                {
                    var value = Guid.Parse(claim.Value);
                    additionalProfileIds = additionalProfileIds == null ? value.AsEnumerable() : additionalProfileIds.Append(value);
                    break;
                }

                case JwtClaimTypes.JwtId:
                    tokenId = claim.Value;
                    break;

                case JwtClaimTypes.Scope:
                    scope = scope == null ? claim.Value.AsEnumerable() : scope.Append(claim.Value);
                    break;

                case JwtClaimTypes.Role:
                case "http://schemas.microsoft.com/ws/2008/06/identity/claims/role":
                    if (Enum.TryParse(typeof(EntityRoleId), claim.Value, out var role))
                    {
                        entityRole = (EntityRoleId)role;
                    }

                    break;

                case "client_account_id": // partner
                case "pi_account_id":
                    accountId = Guid.Parse(claim.Value);
                    break;

                case "pi_org_id":
                    organizationId = Guid.Parse(claim.Value);
                    break;

                case "pi_ghost":
                    break;
            }
        }
        
        var otherProfileIds = profileId.HasValue && additionalProfileIds != null ? additionalProfileIds.Where(x => x != profileId.Value).ToArray() : null;

        IContextWithActor actorContext;
        switch (entityRole)
        {
            case EntityRoleId.Manager:
            case EntityRoleId.User:
                if (!organizationId.HasValue || !userId.HasValue) throw new HttpRequestException("Invalid Token");
                actorContext = UserContext.OrgUser
                (
                    userId.Value,
                    name,
                    entityRole.Value,
                    organizationId.Value,
                    accountId,
                    clientId,
                    profileId,
                    contextClaims,
                    otherProfileIds: otherProfileIds
                ).With(new APIActor(accountId.Value, userId.Value, clientId, tokenId, requestId));
                break;

            case EntityRoleId.Admin:
                if (!accountId.HasValue || !userId.HasValue) throw new HttpRequestException("Invalid Token");
                actorContext = UserContext.Admin
                (
                    userId.Value,
                    name,
                    accountId.Value,
                    clientId,
                    profileId,
                    contextClaims,
                    otherProfileIds: otherProfileIds
                ).With(new APIActor(accountId.Value, userId.Value, clientId, tokenId, requestId));
                break;

            case EntityRoleId.Profile:
                actorContext = ProfileContext.Create
                (
                    profileId.Value,
                    accountId.Value,
                    userId.Value,
                    clientId,
                    organizationId,
                    contextClaims,
                    otherProfileIds: otherProfileIds
                ).With(new APIActor(accountId.Value, userId.Value, clientId, tokenId, requestId));
                break;

            default:
                if (scope != null && scope.Any(x => x is "partner" or "client_app") && accountId.HasValue)
                {
                    var partnerActor = new PartnerActor(accountId.Value, clientId, tokenId, requestId, userId);
                    if (organizationId.HasValue)
                    {
                        actorContext = new OrganizationContext(organizationId.Value, accountId.Value)
                        {
                            ClientId = clientId,
                            ProfileId = profileId,
                            AllProfileIds = profileId.HasValue ? [profileId.Value] : [],
                        }.With(partnerActor);
                    }
                    else
                    {
                        actorContext = new AccountContext
                        {
                            AccountId = accountId.Value,
                            ClientId = clientId,
                            ProfileId = profileId,
                            AllProfileIds = profileId.HasValue ? [profileId.Value] : [],
                        }.With(partnerActor);
                    }
                }
                else
                {
                    throw new HttpRequestException("Unexpected token");
                }

                break;
        }

        return actorContext;
    }
}