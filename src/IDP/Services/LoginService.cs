using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class LoginResult
{
    public User User { get; set; }
    public User Impersonator { get; set; }
    public Guid ProfileId { get; set; }
}

public class LoginCandidateLookup
{
    public AppClient Client { get; set; }
    public ExternalIdentity UserIdentity { get; set; }
    public IList<UserCandidate> Candidates { get; set; }
}

public class UserCandidate
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Description { get; set; }
}

public class LoginService
{
    private readonly ILogger<LoginService> _logger;
    private readonly AuthorizationService _authorizationService;
    private readonly ObjectTypeService _objectTypeService;
    private readonly MongoConnection _connection;
    private readonly IMapper _mapper;
    private readonly Dictionary<string, IIdentityProvider> _providers;
    private readonly GenericIdentityProvider _genericProvider;

    public LoginService(
        ILogger<LoginService> logger,
        MongoConnection connection,
        IMapper mapper,
        AuthorizationService authorizationService,
        ObjectTypeService objectTypeService,
        IEnumerable<IIdentityProvider> providers,
        GenericIdentityProvider genericProvider
    )
    {
        _logger = logger;
        _authorizationService = authorizationService;
        _objectTypeService = objectTypeService;
        _connection = connection;
        _mapper = mapper;
        _providers = providers.ToDictionary(x => x.Name);
        _genericProvider = genericProvider;
    }

    public async Task<LoginResult> LoginUserAsync(ExternalLoginInfo loginInfo, string clientId, IEnumerable<string> acrValues, Guid? selectedUserId = null)
    {
        using var scope = _logger.AddScope(new
        {
            CientId = clientId,
            loginInfo.LoginProvider,
            loginInfo.ProviderKey,
        });

        _logger.LogInformation("Initiate Login");

        var client = await GetClientAsync(clientId, loginInfo);
        if (client == null) return null;

        var (user, profileId) = await LoginUserAsync(client, loginInfo, selectedUserId);
        if (user == null)
        {
            return null;
        }

        // hack to allow impersonation
        var actorId = acrValues?.FirstOrDefault(x => x.StartsWith("impersonate:"));
        if (string.IsNullOrEmpty(actorId))
        {
            return profileId.HasValue
                ? new LoginResult
                {
                    User = user,
                    ProfileId = profileId.Value,
                    Impersonator = null,
                }
                : null;
        }

        var parts = actorId.Split(":");
        if (parts.Length == 2 && Guid.TryParse(parts[1], out var id))
        {
            var impersonatedUser = await FindAsync(user.Context, id);
            if (impersonatedUser == null)
            {
                _logger.LogError("Couldn't find {UserId} to impersonate", id);
                return null;
            }

            var profile = await _authorizationService.GetProfileAsync(impersonatedUser, client);
            if (profile == null)
            {
                _logger.LogError("Trying to impersonate {UserId} that doesn't have access to {ClientId}", id, clientId);
                return null;
            }

            _logger.LogInformation("{UserId} impersonating {ImpersonatedUserId}", user.Id, impersonatedUser.Id);

            return new LoginResult
            {
                User = impersonatedUser,
                ProfileId = profile.Id,
                Impersonator = user,
            };
        }

        return null;
    }

    private async Task<(User User, Guid? ProfileId)> LoginUserAsync(AppClient client, ExternalLoginInfo loginInfo, Guid? selectedUserId = null)
    {
        var provider = ResolveProvider(client, loginInfo);
        if (provider == null) return (null, null);

        var userIdentity = await provider.GetIdentityAsync(loginInfo);
        if (userIdentity == null)
        {
            _logger.LogError("Can't figure out user Identity");
            return (null, null);
        }

        var profileKey = client.ProfileKey ?? client.ClientId;

        User user;
        if (selectedUserId.HasValue)
        {
            // Picker round-trip: re-run the candidate query and verify the posted id is in the set.
            // Never trust the form-posted id alone.
            var candidates = await FindCandidateUsersAsync(client, userIdentity);
            user = candidates.FirstOrDefault(c => c.Id == selectedUserId.Value);
            if (user == null)
            {
                _logger.LogError("Selected {UserId} not in candidate set", selectedUserId.Value);
                return (null, null);
            }
        }
        else
        {
            user = await GetUserAsync(client, userIdentity);
            if (user == null)
            {
                return await autoProvisionUserAsync();
            }
        }

        // is it an invitation?
        // TODO: add something explicit for it?
        // ...
        if (!user.IsActive && user.Identities == null)
        {
            var invitedUser = await autoAcceptInvitationAsync(user);
            if (invitedUser == null)
            {
                _logger.LogInformation("Failed to accept invitation {UserId}", user.Id);
                return (null, null);
            }

            user = invitedUser;
        }

        if (!user.IsActive || user.UserRoleId == nameof(EntityRoleId.Disabled))
        {
            _logger.LogWarning("Trying to login with inactive");
            return (null, null);
        }

        user = await UpdateExternalIdentityAsync(user, userIdentity);

        // profile defined for user
        if (user.AppProfiles?.TryGetValue(profileKey, out var profileId) ?? false)
        {
            _logger.LogInformation("Found {ProfileId} in user.AppProfiles", profileId);
            return (user, profileId);
        }

        return await autoProvisionClientAsync();

        async Task<User> autoAcceptInvitationAsync(User invitedUser)
        {
            using var scope = _logger.AddScope(new
            {
                UserId = invitedUser.Id,
                userIdentity.Email,
                userIdentity.IsVerifiedEmail,
            });

            _logger.LogInformation("Accepting invitation");

            var identity = _mapper.Map<EntityIdentity>(userIdentity);

            invitedUser = await _connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, invitedUser.AccountId)
                .Eq(x => x.Id, invitedUser.Id)
                .Eq(x => x.Identities, null)
                .Eq(x => x.IsActive, false)
                .Update
                .Set(x => x.IsActive, true)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Push(x => x.Identities, identity)
                .UpdateAndGetOneAsync();

            await _objectTypeService.FireObjectUpdatedAsync(
                invitedUser.Context,
                invitedUser,
                new Dictionary<string, object>
                {
                    { nameof(User.IsActive), true },
                    { nameof(User.Identities), "[...]" },
                },
                e => { e.Description = "User accepted invitation"; }
            );

            return invitedUser;
        }

        async Task<(User User, Guid? ProfileId)> autoProvisionUserAsync()
        {
            var tenantIdentity = await provider.GetTenantAsync(loginInfo, userIdentity);
            var tenantId = tenantIdentity?.ExternalId;
            var account = client.AccountId.HasValue
                ? await _connection.Filter<Entity, Account>()
                    .Eq(x => x.Id, client.AccountId.Value)
                    .Ne(x => x.IsActive, false)
                    .FirstOrDefaultAsync()
                : null;

            account ??= tenantIdentity != null ? await GetAccountAsync(client, tenantIdentity) : null;

            // if there wasn't a tenant and there is an account, get from it
            tenantId ??= account?.FirstIdentity(loginInfo.LoginProvider)?.ExternalId;

            using var scope = _logger.AddScope(new
            {
                AccountId = account?.Id,
                TenantId = tenantId,
            });

            _logger.LogInformation("Auto provision user");

            if (!(client.AuthenticationProviders?.TryGetValue(loginInfo.LoginProvider, out var authLoginProvider) ?? false))
            {
                _logger.LogInformation("{LoginProvider} not configured for client", loginInfo.LoginProvider);
                return (null, null);
            }

            if (!(tenantId != null && (authLoginProvider.Tenants?.TryGetValue(tenantId, out var tenant) ?? false)) && !(authLoginProvider.Tenants?.TryGetValue("*", out tenant) ?? false))
            {
                _logger.LogInformation("{TenantId} not configured for provider/client", tenantId);
                return (null, null);
            }

            if (tenant.AutoProvisionUser?.UserRole == null)
            {
                _logger.LogInformation("Do not allow auto provision of new users for this tenant/provider/client");
                return (null, null);
            }

            var userRoleStr = tenant.AutoProvisionUser.UserRole.ToString();
            if (tenant.AppProfiles == null || !tenant.AppProfiles.TryGetValue(userRoleStr, out var profile))
            {
                profile = null;

                _logger.LogError("No profile config for {UserRole} in tenant", tenant.AutoProvisionUser.UserRole);

                // try to get default for role/client
                client.AppProfiles?.TryGetValue(userRoleStr, out profile);
            }

            account ??= tenant.AccountId.HasValue
                ? await _connection.Filter<Entity, Account>()
                    .Eq(x => x.Id, tenant.AccountId.Value)
                    .Ne(x => x.IsActive, false)
                    .FirstOrDefaultAsync()
                : null;

            if (account == null)
            {
                if (tenant.AutoProvisionUser.UserRole == EntityRoleId.Admin)
                {
                    // TODO: auto provision account?
                    // ...
                }

                _logger.LogError("Couldn't determine account");
                return (null, null);
            }

            var resolvedProfileId = profile != null ? await ResolveProfileAsync(account.Id, profile) : null;

            // create new user
            var newUser = provider.BuildUser(account, userIdentity);
            newUser.UserRoleId = userRoleStr;
            newUser.FlowId = tenant.AutoProvisionUser.UserFlowId;
            // user.ObjectStatusId =
            newUser.AppProfiles ??= new Dictionary<string, Guid>();
            if (resolvedProfileId.HasValue)
            {
                newUser.AppProfiles[profileKey] = resolvedProfileId.Value;
            }

            if (newUser.UserRoleId == nameof(EntityRoleId.Manager))
            {
                // auto provision org for new manager
                var org = new Organization
                {
                    AccountId = account.Id,
                    EntityId = account.Id,
                    Id = Guid.NewGuid(),
                    CreatedOn = DateTime.UtcNow,
                    Name = newUser.Name,
                    Description = $"{newUser.Name}'s Organization",
                    Email = newUser.Email,
                    Phone = newUser.Phone,
                    TimeZoneId = newUser.TimeZoneId,
                    FlowId = tenant.AutoProvisionUser.OrganizationFlowId,
                    // ObjectStatusId =
                    // Identities = 
                };

                await _objectTypeService.InsertAsync(account.Context, org, e =>
                {
                    e.Description ??= "Organization Provisioned on Login";
                    e.Action ??= "ObjectCreated";
                });

                newUser.OrganizationId = org.Id;
            }

            await _objectTypeService.InsertAsync(account.Context, newUser, e =>
            {
                e.Description ??= $"User Provisioned on Login";
                e.Action ??= "ObjectCreated";
            });

            return (newUser, resolvedProfileId);
        }

        async Task<(User User, Guid? ProfileId)> autoProvisionClientAsync()
        {
            _logger.LogInformation("Auto provision client");

            var tenantIdentity = await provider.GetTenantAsync(loginInfo, userIdentity);
            if (tenantIdentity != null && client.AuthenticationProviders.TryGetValue(loginInfo.LoginProvider, out var authProvider))
            {
                if (authProvider.Tenants?.TryGetValue(tenantIdentity.ExternalId, out var tenant) ?? false)
                {
                    if (tenant.AppProfiles?.TryGetValue(user.UserRoleId, out var profile) ?? false)
                    {
                        return await autoProvisionClientWithAsync(profile);
                    }
                }
            }

            // fallback to role profile defined for client
            if (client.AppProfiles?.TryGetValue(user.UserRoleId, out var roleProfile) ?? false)
            {
                return await autoProvisionClientWithAsync(roleProfile);
            }

            _logger.LogInformation("Didn't find a profile for the client");
            return (null, null);
        }

        async Task<(User User, Guid? ProfileId)> autoProvisionClientWithAsync(AppClientProfile profile)
        {
            var found = await ResolveProfileAsync(user.AccountId, profile);
            if (!found.HasValue)
            {
                _logger.LogError("Didn't find {ProfileId} / {ProfileName}", profile.Id, profile.Name);
                return (null, null);
            }

            _logger.LogInformation("Auto provision client for user with {ProfileId}", found.Value);

            user = await _connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, user.AccountId)
                .Eq(x => x.Id, user.Id)
                .Update
                .Set(x => x.AppProfiles[profileKey], found.Value)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateAndGetOneAsync();

            // TODO: fire event?
            // ...

            return (user, found.Value);
        }
    }

    private async ValueTask<Guid?> ResolveProfileAsync(Guid accountId, AppClientProfile profile)
    {
        if (profile.Id.HasValue) return profile.Id.Value;

        // lookup by name (for account or fallback)
        var profileByName = await _connection.Filter<AppProfile>()
            .Eq(x => x.Name, profile.Name)
            .In(x => x.AccountId, new[] { accountId, Guid.Empty })
            .SortAsc(x => x.AccountId)
            .FirstOrDefaultAsync();

        return profileByName?.Id;
    }

    public async Task<LoginCandidateLookup> FindLoginCandidatesAsync(ExternalLoginInfo loginInfo, string clientId)
    {
        using var scope = _logger.AddScope(new
        {
            CientId = clientId,
            loginInfo.LoginProvider,
            loginInfo.ProviderKey,
        });

        var client = await GetClientAsync(clientId, loginInfo);
        if (client == null) return null;

        var provider = ResolveProvider(client, loginInfo);
        if (provider == null) return null;

        var userIdentity = await provider.GetIdentityAsync(loginInfo);
        if (userIdentity == null)
        {
            _logger.LogError("Can't figure out user Identity");
            return null;
        }

        var users = await FindCandidateUsersAsync(client, userIdentity);

        if (users.Count < 2)
        {
            return new LoginCandidateLookup
            {
                Client = client,
                UserIdentity = userIdentity,
                Candidates = users
                    .Select(u => new UserCandidate
                    {
                        Id = u.Id,
                        Name = u.Name,
                        Email = u.Email,
                    })
                    .ToList(),
            };            
        }

        var entityIds = users
                    .Where(x => x.OrganizationId.HasValue)
                    .Select(u => u.OrganizationId.Value)
                    .Concat(users.Select(x => x.AccountId))
                ;

            var entities = (await _connection.Filter<Entity>()
                .In(x => x.Id, entityIds)
                .FindAsync()).ToDictionary(x => x.Id, x => x.Name);

            var profileIds = users
                .Where(x => x.UserRoleId == nameof(EntityRoleId.Profile))
                .Select(x => x.AppProfiles.TryGetValue(client.ProfileKey ?? client.ClientId, out var profileId) ? profileId : default(Guid?))
                .Where(x => x.HasValue)
                .ToArray();

            var profiles = profileIds.IsEmpty()
                ? new Dictionary<Guid, string>()
                : (await _connection.Filter<AppProfile>().In(x => x.Id, profileIds).FindAsync())
                .ToDictionary(x => x.Id, x => x.Name);
            
        return new LoginCandidateLookup
        {
            Client = client,
            UserIdentity = userIdentity,
            Candidates = users
                .Select(u => new UserCandidate
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Description = getDescription(u),
                })
                .ToList(),
        };

        string getDescription(User u)
        {
            var description = u.UserRoleId switch
            {
                nameof(EntityRoleId.Profile) => u.AppProfiles.TryGetValue(client.ProfileKey ?? client.ClientId, out var profileId) &&
                                                profiles.TryGetValue(profileId, out var name)
                    ? name
                    : "Unknown Profile",
                _ => u.UserRoleId,
            };

            if (!client.AccountId.HasValue)
            {
                description += " @ " + (entities.TryGetValue(u.AccountId, out var account) ? account : "Unknown Account");
            }

            if (u.OrganizationId.HasValue && entities.TryGetValue(u.OrganizationId.Value, out var organization))
            {
                description += $" ({organization})";
            }

            return description;
        }
    }

    private async Task<AppClient> GetClientAsync(string clientId, ExternalLoginInfo loginInfo)
    {
        var client = await _connection.Filter<AppClient>()
            .Eq(x => x.ClientId, clientId)
            .Ne(x => x.Enabled, false)
            .Ne(x => x.AuthenticationProviders[loginInfo.LoginProvider], null)
            .FirstOrDefaultAsync();

        if (client == null)
        {
            _logger.LogError("Client not found or provider not supported by it");
        }

        return client;
    }

    private IIdentityProvider ResolveProvider(AppClient client, ExternalLoginInfo loginInfo)
    {
        if (_providers.TryGetValue(loginInfo.LoginProvider, out var provider))
        {
            return provider;
        }

        // Fall back to the generic identity provider for tenant-defined OIDC/OAuth2 providers
        // where the dictionary key (e.g. "acme-corp-sso") doesn't match any named built-in.
        var ap = client.AuthenticationProviders.GetValueOrDefault(loginInfo.LoginProvider);
        if (ap?.Type is "oidc" or "oauth2")
        {
            return _genericProvider;
        }

        _logger.LogError("Unexpected {Provider}", loginInfo.LoginProvider);
        return null;
    }

    private async Task<List<User>> FindCandidateUsersAsync(AppClient client, ExternalIdentity userIdentity)
    {
        // Broad identity match (mirrors GetUserAsync's fallback query) restricted to active users.
        // Inactive/invitation users are excluded so the picker never offers a row that can't sign in.
        var query = _connection.Filter<Entity, User>()
            .ElemMatchBuilder(x => x.Identities,
                f => f
                    .Eq(i => i.IdentityProviderId, userIdentity.Provider)
                    .Eq(i => i.ExternalId, userIdentity.ExternalId)
            )
            .Ne(x => x.IsActive, false)
            .Ne(x => x.UserRoleId, nameof(EntityRoleId.Disabled));

        if (client.AccountId.HasValue)
        {
            query.Eq(x => x.AccountId, client.AccountId);
        }

        var users = await query.FindAsync();
        
        // remove profile users without profile for client
        users.RemoveAll(x =>
            x.UserRoleId == nameof(EntityRoleId.Profile) &&
            (x.AppProfiles == null || !x.AppProfiles.ContainsKey(client.ProfileKey ?? client.ClientId))
        );

        return users;
    }

    private async Task<User> GetUserAsync(AppClient client, ExternalIdentity userIdentity)
    {
        var profileKey = client.ProfileKey ?? client.ClientId;

        // main search: user already provisioned with access to client
        var query = _connection.Filter<Entity, User>()
            .ElemMatchBuilder(x => x.Identities,
                f => f
                    .Eq(i => i.IdentityProviderId, userIdentity.Provider.ToString())
                    .Eq(i => i.ExternalId, userIdentity.ExternalId)
            );

        if (client.AccountId.HasValue)
        {
            query.Eq(x => x.AccountId, client.AccountId);
        }
        else
        {
            query.Ne($"{nameof(User.AppProfiles)}.{profileKey}", default(Guid?));
        }

        var user = await query.FirstOrDefaultAsync();
        if (user != null)
        {
            _logger.LogInformation("Found {UserId}", user.Id);
            return user;
        }

        // invitation: didn't find exact identity but there is a place holder use (no identities)
        // with a matching email with a profile for the client 
        if (!string.IsNullOrWhiteSpace(userIdentity.Email))
        {
            query = _connection.Filter<Entity, User>()
                    .Ne($"{nameof(User.AppProfiles)}.{profileKey}", default(Guid?))
                    .Eq(x => x.Email, userIdentity.Email)
                    .Eq(x => x.Identities, null)
                    .Eq(x => x.IsActive, false)
                // TODO: something explicit about it being an invitation
                // ...
                // .Gte(x => x.CreatedOn, DateTime.UtcNow.AddDays(-3))
                ;

            if (client.AccountId.HasValue) query.Eq(x => x.AccountId, client.AccountId.Value);

            user = await query.FirstOrDefaultAsync();
            if (user != null)
            {
                _logger.LogInformation("Found {Email} invitation: {UserId}", userIdentity.Email, user?.Id);
                return user;
            }
        }

        // fallback to identity without requiring that the user have already a profile defined for the client
        // it will try to provision the client for user later
        query = _connection.Filter<Entity, User>()
            .ElemMatchBuilder(x => x.Identities,
                f => f
                    .Eq(i => i.IdentityProviderId, userIdentity.Provider.ToString())
                    .Eq(i => i.ExternalId, userIdentity.ExternalId)
            );

        if (client.AccountId.HasValue)
        {
            query.Eq(x => x.AccountId, client.AccountId);
        }

        user = await query.FirstOrDefaultAsync();
        if (user != null)
        {
            _logger.LogInformation("Found {UserId} {IsActive} {Email}", user.Id, user.IsActive, user.Email);
        }

        return user;
    }

    /// <summary>
    /// Update identity for user
    /// </summary>
    private async Task<User> UpdateExternalIdentityAsync(User user, ExternalIdentity externalIdentity)
    {
        var providerId = externalIdentity.Provider.ToString();

        var result = await _connection.Filter<User>()
            .Eq(x => x.Id, user.Id)
            .ElemMatchBuilder(
                x => x.Identities,
                f => f
                    .Eq(i => i.IdentityProviderId, providerId)
                    .Eq(x => x.ExternalId, externalIdentity.ExternalId)
            )
            .Update
            .Set($"{nameof(Entity.Identities)}.$.{nameof(EntityIdentity.ExternalIdentity)}", externalIdentity)
            .UpdateAndGetOneAsync();

        if (result == null)
        {
            _logger.LogError("Couldn't update user");
            return null;
        }

        if (string.IsNullOrEmpty(result.Email) && !string.IsNullOrEmpty(externalIdentity.Email) && externalIdentity.IsVerifiedEmail)
        {
            result = await _connection.Filter<User>()
                .Eq(x => x.Id, result.Id)
                .Update
                .Set(x => x.Email, externalIdentity.Email)
                .UpdateAndGetOneAsync();
        }

        return result;
    }

    /// <summary>
    /// Find account using "tenant" with access to client
    /// </summary>
    private async Task<Account> GetAccountAsync(AppClient client, ExternalIdentity identity)
    {
        var account = await _connection.Filter<Entity, Account>()
            .Ne(x => x.IsActive, false)
            .ElemMatchBuilder(x => x.Identities,
                f => f
                    .Eq(i => i.IdentityProviderId, identity.Provider.ToString())
                    .Eq(i => i.ExternalId, identity.ExternalId)
            ).FirstOrDefaultAsync();

        return account;
    }

    private async Task<User> FindAsync(IEntityContext context, Guid id)
    {
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            _logger.LogError("Couldn't find {userId} in {accountId}", id, context.AccountId.Value);
            return null;
        }

        if (user.Context.Role == context.Role)
        {
            // do not allow to impersonate other user on the same role
            _logger.LogError("Can't impersonate user in the same level");
            return null;
        }

        if (context.Role switch
            {
                EntityRoleId.Admin => user.UserRoleId == nameof(EntityRoleId.Manager) || user.UserRoleId == nameof(EntityRoleId.User),
                _ => false,
            })
        {
            return user;
        }

        _logger.LogError("{UserId} can't impersonate {OtherUserId}", context.UserId.Value, user.Id);
        return null;
    }
}