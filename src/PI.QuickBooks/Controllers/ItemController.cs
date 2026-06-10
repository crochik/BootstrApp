using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Intuit.Ipp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using PI.ProductCatalog.Models;
using PI.QuickBooks.Models;
using PI.QuickBooks.Services;
using PI.Shared.Controllers;
using PI.Shared.Extensions;
using PI.Shared.Models;
using Account = Intuit.Ipp.Data.Account;

namespace Controllers;

[Produces("application/json")]
[Route("/quickbooks/v1/[controller]")]
public class ItemController : APIController
{
    private readonly MongoConnection _connection;
    private readonly QuickBooksService _service;

    public ItemController(MongoConnection connection, QuickBooksService service)
    {
        _connection = connection;
        _service = service;
    }

    [Authorize("admin")]
    [HttpGet("Organization({entityId})")]
    public async Task<IEnumerable<Item>> GetAsync([FromRoute] Guid entityId)
    {
        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, entityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        return await _service.GetAllItemsAsync(entity.Context);
    }

    [Authorize("admin")]
    [HttpGet("Organization({entityId})/Item")]
    public async Task<IEnumerable<Item>> GetItemAsync([FromRoute] Guid entityId, [FromQuery] string name)
    {
        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, entityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        return await _service.FindAsync<Item>(entity.Context, nameof(Item.Name), name);
    }

    /// <summary>
    /// Export all items for feed
    /// Mainly for Accessories and Labor 
    /// </summary>
    [Authorize("admin")]
    [HttpPost("CatalogFeed({catalogFeedId})/Items")]
    public async Task<IEnumerable<QbEntity>> AddItemsAsync([FromRoute] Guid catalogFeedId, [FromQuery] bool forceUpdate)
    {
        var feed = await _connection.Filter<CatalogFeed>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, catalogFeedId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, feed.EntityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        var context = entity.Context.WithActorFrom(Context);

        return await _service.ExportAllItemsAsync(context, feed, forceUpdate);
    }
}