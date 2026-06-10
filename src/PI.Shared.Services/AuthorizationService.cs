using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Crochik.Security;
using IdentityModel;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using User = PI.Shared.Models.User;

namespace PI.Shared.Services;

public class AuthorizationService
{
    private readonly ILogger<AuthorizationService> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private string Authority { get; }
    private string Audience { get; }
    private Microsoft.IdentityModel.Tokens.SigningCredentials Credentials { get; }

    public AuthorizationService(
        ILogger<AuthorizationService> logger,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        MongoConnection connection,
        ObjectTypeService objectTypeService
    )
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;

        var dataprotection = configuration.GetDataProtectionConfig();
        if (!dataprotection.UseAWS) throw new System.Exception("Data Protection not enabled");

        var rsakey = AWSSystemManagerHelper.GetParameter(configuration, dataprotection.DeveloperSigningCredential);
        var serializer = new RSAKeySerializer();
        Credentials = serializer.GetSigningCredentials("", rsakey);

        var authConfig = PI.Shared.App.MicroserviceApp.AuthenticationConfig.Get(configuration);
        Authority = authConfig.Authority;
        Audience = authConfig.APIName;
    }

    private Result<string> GeneratePartnerToken(IEntityContext context, string clientId)
    {
        var claims = new List<Claim>();
        claims.Add(new Claim(JwtClaimTypes.ClientId, clientId));
        claims.Add(new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()));
        claims.Add(new Claim("client_account_id", context.AccountId.Value.ToString()));
        claims.Add(new Claim("scope", "partner"));

        if (context.OrganizationId.HasValue) claims.Add(new Claim("pi_org_id", context.OrganizationId.Value.ToString()));

        return GenerateJwtToken(claims);
    }

    /// <summary>
    /// Impersonate User 
    /// </summary>
    public async Task<Result<string>> ImpersonateUserAsync(IEntityContext context, User user, TimeSpan? expiration = null)
    {
        if (user == null) return Result.Error<string>("Invalid User");
        var canAccess = context.Role switch
        {
            EntityRoleId.Admin => user.UserRoleId switch
            {
                nameof(EntityRoleId.Admin) => false, // user.AccountId == context.AccountId,
                nameof(EntityRoleId.Manager) => user.AccountId == context.AccountId,
                nameof(EntityRoleId.User) => user.AccountId == context.AccountId,
                nameof(EntityRoleId.Profile) => user.AccountId == context.AccountId,
                _ => false,
            },
            EntityRoleId.Manager => user.UserRoleId switch
            {
                nameof(EntityRoleId.Manager) => false, // user.OrganizationId == context.OrganizationId,
                nameof(EntityRoleId.User) => user.OrganizationId == context.OrganizationId,
                _ => false,
            },
            _ => false,
        };

        // allow user to impersonate ghost
        canAccess |= context.UserId.HasValue && (user.FirstIdentity(ExternalProvider.Bootstrapp)?.ExternalId == context.UserId.Value.ToString());

        if (!canAccess) return Result.Error<string>("Unauthorized");

        var claimsResult = await GetAllClaimsAsync(user, context.ClientId);
        if (!claimsResult)
        {
            _logger.LogError("Failed to generate claims for {UserId}: {Error}", user.Id, claimsResult.Status);
            throw new ForbiddenException(claimsResult.Status);
        }

        var claims = claimsResult.Value
            .Append(
                new Claim("pi_impersonator_id", context.UserId.ToString())
            );

        var ghostIds = await GetGhostUserIdsAsync(user);
        claims = claims.Concat(ghostIds.Select(x => new Claim("pi_ghost", $"{x.Id}:{x.OrganizationId}")));

        var jwt = GenerateJwtToken(claims, expiration ?? TimeSpan.FromHours(4));
        if (!jwt)
        {
            _logger.LogError("Failed to generate Jwt Token for {UserId}: {Error}", user.Id, jwt.Status);
            throw new ForbiddenException(jwt.Status);
        }

        return Result.Success(jwt.Value);
    }

    public async Task<Result<string>> GenerateJwtTokenAsync(User user, string clientId = null, TimeSpan? expiration = null, Func<AppClient, bool> clientCheck = null)
    {
        if (user == null) return Result.Error<string>("Invalid User");

        var claims = await GetAllClaimsAsync(user, clientId, clientCheck);
        if (!claims)
        {
            _logger.LogError("Failed to generate claims for {UserId}", user.Id);
            throw new ForbiddenException("Invalid Client");
        }

        var jwt = GenerateJwtToken(claims.Value, expiration ?? TimeSpan.FromHours(4));
        if (!jwt)
        {
            _logger.LogError("Failed to generate Jwt Token for {UserId}", user.Id);
            throw new ForbiddenException(jwt.Status);
        }

        return Result.Success(jwt.Value);
    }

    /// <summary>
    /// Generate all claims similar to what the IdentiyProvider4 would
    /// </summary>
    private async Task<Result<IEnumerable<Claim>>> GetAllClaimsAsync(User user, string clientId, Func<AppClient, bool> clientCheck = null)
    {
        using var scope = _logger.AddScope(new
        {
            UserId = user?.Id,
            ClientId = clientId,
        });

        _logger.LogInformation("Get claims for user");

        var idpClient = await _connection.Filter<AppClient>()
            .Eq(x => x.ClientId, clientId)
            .FirstOrDefaultAsync();

        if (idpClient == null)
        {
            _logger.LogError("Client does not exist");
            return Result.Error<IEnumerable<Claim>>("Client Not Found");
        }

        var profile = await GetProfileAsync(user, idpClient);
        if (profile==null)
        {
            _logger.LogError("User has no profile for {ClientId}", clientId);
            return Result.Error<IEnumerable<Claim>>("No Profile");
        }

        if (clientCheck != null && !clientCheck.Invoke(idpClient))
        {
            _logger.LogError("{ClientId} failed validation", clientId);
            return Result.Error<IEnumerable<Claim>>("Invalid Client");
        }

        var claims = await GetAllClaimsAsync(user, profile, idpClient);

        return Result.Success<IEnumerable<Claim>>(claims);
    }

    /// <summary>
    /// Get claims (including calculated)
    /// </summary>
    public async Task<List<Claim>> GetAllClaimsAsync(User user, Guid profileId, AppClient idpClient)
    {
        var profile = await _connection.Filter<AppProfile>()
            .Eq(x => x.Id, profileId)
            .FirstOrDefaultAsync();
        
        if (profile == null) throw new Exception("Invalid Profile");
        return await GetAllClaimsAsync(user, profile, idpClient);
    }
    
    public async Task<List<Claim>> GetAllClaimsAsync(User user, AppProfile profile, AppClient idpClient)
    {
        var claims = GetAllClaims(user, profile, idpClient);

        await AddCalculatedClaimsAsync(claims, user, idpClient, profile);

        return claims;
    }

    /// <summary>
    /// Get ghost users ids 
    /// </summary>
    public async Task<GhostUserId[]> GetGhostUserIdsAsync(User user)
    {
        var ghosts = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, user.AccountId)
            .Ne(x => x.IsActive, false)
            .Eq(x => x.UserRoleId, user.UserRoleId)
            .ElemMatchBuilder(f => f.Identities,
                q => q
                    .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Bootstrapp))
                    .Eq(x => x.ExternalId, user.Id.ToString())
            )
            .IncludeFields(x => x.Id, x => x.OrganizationId)
            .FindAsync<GhostUserId>();

        if (ghosts.Count == 0) return [];

        return ghosts.ToArray();
    }

    /// <summary>
    /// Generate claims similar to what the IdentiyProvider4 would
    /// </summary>
    private static List<Claim> GetAllClaims(User user, AppProfile profile, AppClient idpClient)
    {
        return claims().ToList();

        IEnumerable<Claim> claims()
        {
            var now = DateTime.UtcNow;
            yield return new Claim(JwtClaimTypes.Subject, user.Id.ToString());
            yield return new Claim(JwtClaimTypes.ClientId, idpClient.ClientId);
            yield return new Claim(JwtClaimTypes.AuthenticationTime, now.ToEpochTime().ToString());
            yield return new Claim(JwtClaimTypes.IdentityProvider, nameof(ExternalProvider.Salesforce));
            foreach (var scope in idpClient.AllowedScopes)
            {
                yield return new Claim(JwtClaimTypes.Scope, scope.Scope);
            }

            if (idpClient.Claims != null && idpClient.AlwaysSendClientClaims)
            {
                foreach (var claim in idpClient.Claims)
                {
                    yield return new Claim($"{idpClient.ClientClaimsPrefix ?? ""}{claim.Type}", claim.Value);
                }
            }

            if (idpClient.IncludeJwtId)
            {
                yield return new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N"));
            }

            var mainIdentity = user.MainIdentityId.HasValue ? user.GetIdentities().FirstOrDefault(i => i.Id == user.MainIdentityId.Value) : null;
            if (mainIdentity?.ExternalIdentity != null)
            {
                yield return new Claim(JwtClaimTypes.Name, mainIdentity.ExternalIdentity.Name);
                yield return new Claim(JwtClaimTypes.GivenName, mainIdentity.ExternalIdentity.GivenName);
                yield return new Claim(JwtClaimTypes.FamilyName, mainIdentity.ExternalIdentity.FamilyName);
                yield return new Claim(JwtClaimTypes.Email, mainIdentity.ExternalIdentity.Email);
            }
            else
            {
                // use user info?
                yield return new Claim(JwtClaimTypes.Name, user.Name);
                // yield return new Claim(JwtClaimTypes.GivenName, user.FirstName);
                // yield return new Claim(JwtClaimTypes.FamilyName, user.LastName);
                if (!string.IsNullOrWhiteSpace(user.Email)) yield return new Claim(JwtClaimTypes.Email, user.Email);
            }

            yield return new Claim(JwtClaimTypes.Role, user.UserRoleId.ToString());
            yield return new Claim("pi_account_id", user.AccountId.ToString());
            if (profile != null)
            {
                yield return new Claim("pi_profile_id", profile.Id.ToString("N")); 
                // other profile ids
                if (profile.OtherProfileIds != null)
                {
                    foreach (var id in profile.OtherProfileIds)
                    {
                        yield return new Claim("pi_profile_ids", id.ToString("N"));
                    }
                }
            }

            if (user.OrganizationId.HasValue) yield return new Claim("pi_org_id", user.OrganizationId.ToString());

            // var authConfig = PI.Shared.App.MicroserviceApp.AuthenticationConfig.Get(_configuration);
            // yield return new Claim(JwtClaimTypes.NotBefore, now.ToEpochTime().ToString());
            // yield return new Claim(JwtClaimTypes.Expiration, now.AddHours(8).ToEpochTime().ToString());
            // yield return new Claim(JwtClaimTypes.Issuer, authConfig.Authority);
            // yield return new Claim(JwtClaimTypes.Audience, authConfig.APIName);
        }
    }

    public async Task AddCalculatedClaimsAsync(List<Claim> claims, User user, AppClient client, AppProfile profile)
    {
        if (client?.CalculatedClaims == null || client.CalculatedClaims?.Count < 1)
        {
            return;
            // client.CalculatedClaims = new Dictionary<string, string>
            // {
            //     { "pi_account_id", "{{Objects.Account._id}}" },
            //     { "pi_profile_id", "{{Object.ProfileId}}" },
            //     { "pi_org_id", "{{Objects.Organization._id}}" },
            // };
        }

        var accountContext = new AccountContext(user.AccountId);
        var accountObj = await getObject(nameof(Account), user.AccountId);
        var organizationObj = await getObject(nameof(Organization), user.OrganizationId);
        var userObj = await getObject(nameof(User), user.Id);

        var context = new Dictionary<string, object>
        {
            {
                "Objects", new Dictionary<string, object>
                {
                    { nameof(Account), accountObj },
                    { nameof(Organization), organizationObj },
                    { nameof(User), userObj }
                }
            },
            {
                "Object", new Dictionary<string, object>
                {
                    { "ProfileId", profile?.Id }
                }
            }
        };

        // use evaluation service to calculate claims
        foreach (var kvp in client.CalculatedClaims)
        {
            if (ExpressionEvaluatorService.TryResolve(null, context, kvp.Value, out var value) && value != null)
            {
                _logger.LogInformation("{Claim}: resolved {Expression} into {Value}", kvp.Key, kvp.Value, value);
                AddOrUpdate(claims, kvp.Key, value.ToString());
            }
        }

        async Task<Dictionary<string, object>> getObject(string objectTypeName, Guid? id)
        {
            if (!id.HasValue) return null;

            var objectType = await _objectTypeService.GetAsync(accountContext, objectTypeName);
            if (objectType == null) return null;

            var expando = await _objectTypeService.GetExpandoObjectByIdAsync(accountContext, objectType, id.Value);
            if (expando == null) return null;

            return await _objectTypeService.RecursivelyFlattenAsync(accountContext, objectType, expando);
        }
    }

    private static void AddOrUpdate(List<Claim> claims, string name, string value)
    {
        claims.RemoveAll(c => c.Type == name);
        if (!string.IsNullOrEmpty(value))
        {
            claims.Add(new Claim(name, value));
        }
    }

    public Result<string> GenerateJwtToken(IEnumerable<Claim> claims, TimeSpan? expiration = null)
    {
        // default 30 days?
        expiration ??= TimeSpan.FromDays(30);

        _logger.LogInformation("Generate JWT Token");

        // Create Security Token object by giving required parameters    
        var token = new JwtSecurityToken(
            Authority,
            Audience,
            claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(expiration.Value),
            signingCredentials: Credentials
        );

        var jwtToken = new JwtSecurityTokenHandler().WriteToken(token);

        return Result.Success(jwtToken);
    }

    public ClaimsPrincipal ValidateToken(string token)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = Authority,
                ValidAudience = Audience,
                IssuerSigningKey = Credentials.Key,
            }, out var validatedToken);

            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate token");
            throw new ForbiddenException("Failed to validate token");
        }
    }
    
    public async Task<AppProfile> GetProfileAsync(User user, AppClient client)
    {
        using var scope = _logger.AddScope(new
        {
            UserId = user.Id,
            client.ClientId,
            user.UserRoleId,
        });

        _logger.LogInformation("Try to find profile");

        // check if the user has a custom profile for this client (id)
        var profileKey = client.ProfileKey ?? client.ClientId;
        if (user.AppProfiles != null && user.AppProfiles.TryGetValue(profileKey, out var profileId))
        {
            // overriden by user
            _logger.LogInformation("Found {ProfileId} in user.AppProfiles", profileId);
            
            return await _connection.Filter<AppProfile>()
                .Eq(x=>x.Id, profileId)
                // .In(x => x.AccountId, new[] { user.AccountId, Guid.Empty })
                // .SortAsc(x => x.AccountId)
                .FirstOrDefaultAsync();
        }

        // resolve profileId based on user role 
        if (client.AppProfiles != null)
        {
            // new profile configuration
            if (client.AppProfiles.TryGetValue(user.UserRoleId, out var profile))
            {
                if (profile.Id.HasValue)
                {
                    // fixed id
                    _logger.LogInformation("{ProfileId} defined for role in client", profile.Id);
                    return await _connection.Filter<AppProfile>()
                        .Eq(x=>x.Id, profile.Id)
                        // .In(x => x.AccountId, new[] { user.AccountId, Guid.Empty })
                        // .SortAsc(x => x.AccountId)
                        .FirstOrDefaultAsync();
                }

                _logger.LogInformation("Profile is defined for role, but not by id");

                if (!string.IsNullOrWhiteSpace(profile.Name))
                {
                    _logger.LogInformation("{ProfileName} defined for role in client", profile.Name);

                    // lookup by name (for account or fallback)
                    var profileByName = await _connection.Filter<AppProfile>()
                        .Eq(x => x.Name, profile.Name)
                        .In(x => x.AccountId, new[] { user.AccountId, Guid.Empty })
                        .SortAsc(x => x.AccountId)
                        .FirstOrDefaultAsync();

                    if (profileByName != null)
                    {
                        _logger.LogInformation("Resolved {ProfileName} to {ProfileId}", profile.Name, profileByName.Id);
                        return profileByName;
                    }
                    else
                    {
                        _logger.LogInformation("Did not find profile for {ProfileName}", profile.Name);
                    }
                }
                else
                {
                    _logger.LogError("Profile is defined for role but does not include name or id");
                }
            }
        }

        // fall back to previous profile per role
        var defaultProfileId = user.UserRoleId switch
        {
            nameof(EntityRoleId.Admin) => client.AppClientProfiles?.Admin,
            nameof(EntityRoleId.Manager) => client.AppClientProfiles?.Manager,
            nameof(EntityRoleId.User) => client.AppClientProfiles?.User,
            _ => default,
        };

        _logger.LogInformation("Fallback to old profile resolution by role, got {ProfileId}", defaultProfileId);

        if (!defaultProfileId.HasValue) return null;
        
        return await _connection.Filter<AppProfile>()
            .Eq(x=>x.Id, defaultProfileId)
            // .In(x => x.AccountId, new[] { user.AccountId, Guid.Empty })
            // .SortAsc(x => x.AccountId)
            .FirstOrDefaultAsync();

    }

    public async Task<bool> IsUserActiveAsync(Guid userId, string clientId)
    {
        using var scope = _logger.AddScope(new
        {
            UserId = userId,
            ClientId = clientId,
        });

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.Id, userId)
            .Eq(x => x.IsActive, true)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            _logger.LogInformation("User not found or not active");
            return false;
        }

        var client = await _connection.Filter<AppClient>()
            .Eq(x => x.ClientId, clientId)
            .FirstOrDefaultAsync();

        if (client == null)
        {
            _logger.LogError("No client found");
            throw new NotFoundException($"{clientId}: client not found");
        }

        var profile = await GetProfileAsync(user, client);
        if (profile==null)
        {
            _logger.LogInformation("Can't find profile");
            return false;
        }

        return true;
    }

    public class GhostUserId
    {
        [BsonId]
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
    }
}