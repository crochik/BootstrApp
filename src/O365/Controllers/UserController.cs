using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.O365;
using PI.Shared.O365.Extensions;
using PI.Shared.Services;
using User = Microsoft.Graph.User;

namespace Controllers;

[Route("/o365/v1/[controller]")]
public class UserController : APIController
{
    // https://docs.microsoft.com/en-us/azure/active-directory/enterprise-users/licensing-service-plan-reference
    private const string Office365E1 = "18181a46-0d4e-45cd-891e-60aabd171b4e";
    private const string Office365E2 = "6634e0ce-1a9f-428c-a498-f84ec7b8aa2e";
    private const string Office365E3 = "6fd2c87f-b296-42f0-b197-1e91e994b900";
    private const string Office365E3Developer = "189a915c-fe4f-4ffa-bde4-85b9628d07a0";
    private readonly MongoConnection _connection;
    private readonly O365AuthClient _client;

    public UserController(
        MongoConnection connection,
        O365AuthClient client
    )
    {
        _connection = connection;
        _client = client;
    }

    [HttpGet("User")]
    [Authorize("admin")]
    public async Task<IActionResult> GetUsersAsync()
    {
        var result = await GetUsersAsync(Context);
        return Ok(result);
    }

    [HttpGet("Account({id})/User")]
    [Authorize("root")]
    public async Task<IActionResult> GetUsersForAccountAsync([FromRoute] Guid id)
    {
        var accountContext = new AccountContext(id);
        var result = await GetUsersAsync(accountContext);
        return Ok(result);
    }
        
    private async Task<List<User>> GetUsersAsync(IEntityContext context)
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

        var result = new List<User>();
        await foreach (var user in users)
        {
            result.Add(user);
        }

        return result;
    }
}