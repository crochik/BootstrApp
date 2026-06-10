using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.O365;
using PI.Shared.O365.Extensions;
using PI.Shared.Services;

namespace Services;

public class ImportUsersJob : IRunJob
{
    public string Name => "O365Users";

    // https://docs.microsoft.com/en-us/azure/active-directory/enterprise-users/licensing-service-plan-reference
    private const string PowerBISku = "a403ebcc-fae0-4ca2-8c8c-7a907fd6c235";
    private const string DefenderOffice365 = "4ef96642-f096-40de-a3e9-d83fb2f90211";
    private const string FlowFree = "f30db892-07e9-47e9-837c-80727f46fd3d";
    private const string Office365E1 = "18181a46-0d4e-45cd-891e-60aabd171b4e";
    private const string Office365E2 = "6634e0ce-1a9f-428c-a498-f84ec7b8aa2e";
    private const string Office365E3 = "6fd2c87f-b296-42f0-b197-1e91e994b900";
    private const string Office365E3Developer = "189a915c-fe4f-4ffa-bde4-85b9628d07a0";
    private readonly ILogger<ImportUsersJob> _logger;
    private readonly MongoConnection _connection;
    private readonly O365AuthClient _client;
    private readonly ObjectTypeService _objectTypeService;

    public ImportUsersJob(
        ILogger<ImportUsersJob> logger,
        MongoConnection connection,
        O365AuthClient client,
        ObjectTypeService objectTypeService
    )
    {
        _logger = logger;
        _connection = connection;
        _client = client;
        _objectTypeService = objectTypeService;
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var account = await _connection.Filter<Entity, Account>()
            .Eq(x => x.Id, context.AccountId.Value)
            .FirstOrDefaultAsync();

        if (account == null) throw NotFoundException.New<Account>(context.AccountId.Value);
        if (!account.TryGetMicrosoftTenantId(out var tenanId)) throw new BadRequestException("Unknown tenant");

        var skus = new[]
            {
                Office365E1,
                Office365E2,
                Office365E3,
                Office365E3Developer
            }
            .Select(x => Guid.Parse(x))
            .ToHashSet();

        var users = _client.GetClient(account)
                .Users
                .Request()
                .Select(u => new { u.AssignedLicenses, u.DisplayName, u.Id, u.Mail, u.JoinedTeams, u.OtherMails })
                .Filter("userType eq 'Member'")
                // .Filter($"assignedLicenses/any(u:u/skuId eq {Office365E1})") // userType eq 'Member' AND 
                .Top(999)
                .ReadAll(user => user.AssignedLicenses.Any(x => x.SkuId.HasValue && skus.Contains(x.SkuId.Value)))
            ;

        var objectType = await _objectTypeService.GetObjectTypeAsync<User>(context);

        var usersCreated = 0;
        var total = 0;
        var addedToExisting = 0;
        var skip = 0;
        var failed = 0;

        await foreach (var user in users)
        {
            total++;

            var list = await _connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Ne(x => x.IsActive, false)
                .OrBuilder(
                    q => q.ElemMatchBuilder(
                        e => e.Identities,
                        q1 => q1
                            .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Microsoft))
                            .Eq(x => x.ExternalId, user.Id)
                    ),
                    q => q.Eq(x => x.Email, user.Mail),
                    q => q.ElemMatchBuilder(
                        e => e.Identities,
                        q1 => q1
                            .Eq(x => x.IdentityProviderId, "Salesforce")
                            .In("Data.email", new[] { user.Mail?.ToLowerInvariant() + ".invalid", user.Mail?.ToLowerInvariant() })
                    )
                )
                .FindAsync();

            if (list.IsEmpty())
            {
                // user doesn't exist
                _logger.LogInformation("User not found: {Email} {MicrosoftId}", user.Mail, user.Id);
                // await CreateUserAsync(context, user, objectType, tenanId);
                // usersCreated++;
                continue;
            }

            foreach (var existing in list)
            {
                var microsoft = existing.Identities?.FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.Microsoft));
                if (microsoft != null)
                {
                    if (microsoft.ExternalId == user.Id)
                    {
                        _logger.LogInformation("{Email}: Found {UserId} with {MicrosoftId}: skip", user.Mail, existing.Id, user.Id);
                        skip++;
                    }
                    else
                    {
                        _logger.LogInformation("{Email}: Found {UserId} with {OtherUserId} vs {MicrosoftId}: skip", user.Mail, existing.Id, microsoft.ExternalId, user.Id);
                        failed++;
                    }

                    continue;
                }

                // add identity to existing user
                await UpdateAsync(context, existing, user, objectType, tenanId);
                addedToExisting++;
            }
        }

        return new JobResult
        {
            Message = $"{usersCreated} users created, {addedToExisting} identities added to existing users",
            Result = new Dictionary<string, object>
            {
                { "Total", total },
                { "Created", usersCreated },
                { "Skipped", skip },
                { "Errors", failed },
                { "Updated", addedToExisting },
            }
        };
    }

    private async Task UpdateAsync(IEntityContext context, User user, Microsoft.Graph.User identity, ObjectType objectType, Guid tenantId)
    {
        _logger.LogInformation("Add Identity to existing {UserId} for {O365UserId} - {Email}", user.Id, identity.Id, identity.Mail);

        var userIdentity = new EntityIdentity
        {
            Id = Guid.NewGuid(),
            IdentityProviderId = ExternalProvider.Microsoft.ToString(),
            ExternalIdentity = new ExternalIdentity
            {
                Provider = nameof(ExternalProvider.Microsoft),
                ExternalId = identity.Id,
            },
            ExternalId = identity.Id,
            Name = identity.Mail,
            Data = new Dictionary<string, object>
            {
                { "TenantId", tenantId }
            }
        };

        user = await _connection.Filter<Entity, User>()
            .Eq(x => x.Id, user.Id)
            .Update
            .Push(x => x.Identities, userIdentity)
            .Push(x => x.Integrations, new O365Integration
            {
                UsesAccountAuth = true,
            })
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        await _objectTypeService.FireObjectUpdatedAsync(
            context,
            user,
            new Dictionary<string, object>
            {
                { nameof(User.Integrations), "[...]" },
                { nameof(User.Identities), "[...]" },
            },
            e => { e.Description = "User accepted invitation"; }
        );
    }

    private async Task<User> CreateUserAsync(IEntityContext context, Microsoft.Graph.User identity, ObjectType objectType, Guid tenantId)
    {
        var id = Guid.NewGuid();
        _logger.LogInformation("Create {entityId} for {o365UserId} - {email}", id, identity.Id, identity.Mail);

        var userIdentity = new EntityIdentity
        {
            Id = Guid.NewGuid(),
            IdentityProviderId = ExternalProvider.Microsoft.ToString(),
            ExternalIdentity = new ExternalIdentity
            {
                Provider = nameof(ExternalProvider.Microsoft),
                ExternalId = identity.Id,
            },
            ExternalId = identity.Id,
            Name = identity.Mail,
            Data = new Dictionary<string, object>
            {
                { "TenantId", tenantId }
            }
        };

        var newUser = _objectTypeService.InitObject<User>(context, objectType);
        newUser.MainIdentityId = userIdentity.Id;
        newUser.Name = identity.DisplayName;
        newUser.Email = identity.Mail;
        newUser.UserRoleId = EntityRoleId.Disabled.ToString();
        newUser.IsActive = false;
        newUser.Identities = new[]
        {
            userIdentity,
        };
        newUser.Integrations = new[]
        {
            new O365Integration
            {
                UsesAccountAuth = true,
            }
        };

        return await _objectTypeService.InsertAsync(context, newUser);
    }
}