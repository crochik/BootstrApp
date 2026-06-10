using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.QuickBooks.Services;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using Account = Intuit.Ipp.Data.Account;

namespace Controllers;

[Produces("application/json")]
[Route("/quickbooks/v1/[controller]")]
public class AccountController : APIController
{
    private readonly MongoConnection _connection;
    private readonly QuickBooksService _service;

    public AccountController(MongoConnection connection, QuickBooksService service)
    {
        _connection = connection;
        _service = service;
    }

    /// <summary>
    /// Import QBO accounts into PI for the Org
    /// </summary>
    [Authorize("manager")]
    [HttpPost]
    public async Task<IEnumerable<string>> InitOrgAsync([FromQuery] bool updateExisting = false)
    {
        var localCache = default(LocalCache);
        if (!updateExisting)
        {
            localCache = new LocalCache();
            await _service.LoadAccountsAsync(Context, localCache);
        }

        return await _service.SyncAccountsAsync(Context, localCache, updateExisting);
    }

    /// <summary>
    /// Sync accounts between QBO and PI
    /// - it will export to qbo any account that is expected to be there for the context
    /// - it will not update existing accounts in QBO
    /// </summary>
    [Authorize("admin")]
    [HttpPost("Organization({entityId})")]
    public async Task<IEnumerable<string>> InitAsync([FromRoute] Guid entityId, [FromQuery] bool updateExisting = false)
    {
        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, entityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (entity == null) throw new BadRequestException("Invalid Organization");

        var organizationContext = entity.Context.WithActorFrom(Context);

        var localCache = default(LocalCache);
        if (!updateExisting)
        {
            localCache = new LocalCache();
            await _service.LoadAccountsAsync(organizationContext, localCache);
        }

        return await _service.SyncAccountsAsync(organizationContext, localCache, updateExisting);
    }
}