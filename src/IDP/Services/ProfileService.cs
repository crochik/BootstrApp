using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Crochik.Mongo;
using IdentityModel;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using Microsoft.Extensions.Logging;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;

namespace Services;

// https://identityserver4.readthedocs.io/en/latest/reference/profileservice.html
public class ProfileService : IProfileService
{
    private readonly ILogger<ProfileService> _logger;
    private readonly MongoConnection _connection;
    private readonly AuthorizationService _authorizationService;
    private readonly ObjectTypeService _objectTypeService;

    public ProfileService(
        ILogger<ProfileService> logger,
        MongoConnection connection,
        AuthorizationService authorizationService,
        ObjectTypeService objectTypeService
    )
    {
        _logger = logger;
        _connection = connection;
        _authorizationService = authorizationService;
        _objectTypeService = objectTypeService;
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        var sub = context.Subject.GetSubjectId();
        if (Guid.TryParse(sub, out var id))
        {
            context.IsActive = id == Guid.Empty ||
                               await _authorizationService.IsUserActiveAsync(id, context.Client.ClientId);
        }
        else
        {
            context.IsActive = false;
        }
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var claims = await GetClaimsAsync(context);
        // context.AddRequestedClaims(claims);
        context.IssuedClaims = claims;
    }

    private Task<PI.Shared.Models.User> GetUserByIdAsync(Guid id)
        => _connection.Filter<Entity, User>().Eq(x => x.Id, id).FirstOrDefaultAsync();

    private async Task<List<Claim>> GetClaimsAsync(ProfileDataRequestContext context)
    {
        var subject = context.Subject;
        if (!Guid.TryParse(subject.GetSubjectId(), out var userId))
        {
            _logger.LogError("Invalid Subject: {subjectId}", subject.GetSubjectId());
            return subject.Claims.ToList();
        }

        var user = await GetUserByIdAsync(userId);
        if (user == null)
        {
            _logger.LogError("No user found");
            throw new NotFoundException($"{userId}: user not found");
        }

        var client = await _connection.Filter<AppClient>()
            .Eq(x => x.ClientId, context.Client.ClientId)
            .FirstOrDefaultAsync();

        if (client == null)
        {
            _logger.LogError("No client found");
            throw new NotFoundException($"{context.Client.ClientId}: client not found");
        }

        var profile = await _authorizationService.GetProfileAsync(user, client);

        var claims = subject.Claims.ToList();
        AddStandardClaims(claims, user, profile);
        await _authorizationService.AddCalculatedClaimsAsync(claims, user, client, profile);

        var ghostIds = await _authorizationService.GetGhostUserIdsAsync(user);
        claims.AddRange(ghostIds.Select(x => new Claim("pi_ghost", $"{x.Id}:{x.OrganizationId}")));

        return claims;
    }

    private static void AddStandardClaims(List<Claim> claims, User user, AppProfile profile)
    {
        var mainIdentity = user.MainIdentityId.HasValue
            ? user.GetIdentities().FirstOrDefault(i => i.Id == user.MainIdentityId.Value)
            : null;

        claims.RemoveAll(c =>
            c.Type == JwtClaimTypes.PreferredUserName ||
            c.Type.StartsWith("http", StringComparison.Ordinal)
        );

        // update claims
        if (mainIdentity?.ExternalIdentity != null)
        {
            AddOrUpdate(claims, JwtClaimTypes.Name, mainIdentity.ExternalIdentity.Name);
            AddOrUpdate(claims, JwtClaimTypes.GivenName, mainIdentity.ExternalIdentity.GivenName);
            AddOrUpdate(claims, JwtClaimTypes.FamilyName, mainIdentity.ExternalIdentity.FamilyName);
            AddOrUpdate(claims, JwtClaimTypes.Email, mainIdentity.ExternalIdentity.Email);
        }

        AddOrUpdate(claims, JwtClaimTypes.Role, user.UserRoleId);
        AddOrUpdate(claims, "pi_account_id", user.AccountId.ToString());
        if (profile != null)
        {
            AddOrUpdate(claims, "pi_profile_id", profile.Id.ToString("N"));

            // other profile ids
            if (profile.OtherProfileIds != null)
            {
                claims.RemoveAll(c => c.Type == "pi_profile_ids");
                foreach (var id in profile.OtherProfileIds)
                {
                    claims.Add(new Claim("pi_profile_ids", id.ToString("N")));
                }
            }
        }

        if (user.OrganizationId.HasValue) AddOrUpdate(claims, "pi_org_id", user.OrganizationId.ToString());
    }

    private static void AddOrUpdate(List<Claim> claims, string name, string value)
    {
        claims.RemoveAll(c => c.Type == name);
        if (!string.IsNullOrEmpty(value))
        {
            claims.Add(new Claim(name, value));
        }
    }
}