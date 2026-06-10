using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using PI.ProductCatalog.Models;
using PI.ProductCatalog.Services;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;

namespace Controllers;

[Route("/productcatalog/v1/[controller]")]
public class CatalogFeedController : AbstractCatalogFeedController<CatalogFeed>
{
    public CatalogFeedController(ObjectTypeService objectTypeService) :
        base(objectTypeService)
    {
    }

    [Authorize("admin")]
    [HttpGet("/productcatalog/v1/[controller]({id})/Sync")]
    public async Task<IActionResult> LoadAsync([FromRoute] Guid id, [FromServices] CatalogService service, [FromServices] MongoConnection connection)
    {
        var feed = await connection.Filter<CatalogFeed, B2BCatalogFeed>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (feed == null) throw new NotFoundException(nameof(B2BCatalogFeed), id);

        var success = await service.SyncAsync(Context, feed, null);

        return Ok(success);
    }

    /// Update PlProductSettings, PlMaterialTypeLookup
    /// cascade margins to breadcrumbs
    [Authorize("admin")]
    [HttpPost("/productcatalog/v1/Organization({id})/CascadeSfProductSettings")]
    public async Task<IActionResult> CascadeSfProductSettingsAsync([FromRoute] Guid id, [FromServices] MongoConnection connection)
    {
        var result = await connection.DipperAsync(
            "CascadeMargin",
            $"{Context.AccountId:N}",
            new
            {
                OrganizationId = id.AsSerializedId(),
            });

        return Ok(result);
    }

    /// Cascade margins, 
    // [Authorize("admin")]
    // [HttpPost("/productcatalog/v1/[controller]({id})/AfterSync")]
    // public async Task<IActionResult> RunAfterSyncAsync([FromRoute] Guid id, [FromServices] MongoConnection connection)
    // {
    //     var result = await connection.DipperAsync(
    //         "AfterSync",
    //         "productCatalog",
    //         new
    //         {
    //             CatalogFeedId = id.AsSerilizedId()
    //         });
    //     return Ok(result);
    // }

    [Authorize("managerplus")]
    [HttpPost("Lookup")]
    public async Task<IEnumerable<ReferenceValue>> LookupAsync(DataViewRequest request, [FromServices] MongoConnection connection)
    {
        var entityId = default(Guid?);

        if (request.Criteria.TryGetUidValueFromEqCondition(nameof(CatalogFeed.EntityId), out var accountId) && accountId == Context.AccountId.Value && Context.Role == EntityRoleId.Manager)
        {
            // little hack, allow managers to see list of account catalog feeds
            entityId = Context.AccountId.Value;
        }
        else
        {
            entityId = Context.Role switch
            {
                EntityRoleId.Account => Context.AccountId.Value,
                EntityRoleId.Admin => Context.AccountId.Value,
                EntityRoleId.Organization => Context.OrganizationId.Value,
                EntityRoleId.Manager => Context.OrganizationId.Value,
                _ => throw new ForbiddenException(Context)
            };
        }

        var query = connection.Filter<CatalogFeed>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, entityId.Value);

        if (request.Criteria.TryGetEqCondition(nameof(CatalogFeed.IsActive), out var isActiveCondition))
        {
            query.Eq(x => x.IsActive, true);
        }

        if (request.Criteria.TryGetEqCondition("ObjectType", out var objectTypeCond) && objectTypeCond.Value is string objectType)
        {
            switch (objectType)
            {
                case nameof(XLSCatalogFeed):
                    query.OfType<CatalogFeed, XLSCatalogFeed>();
                    break;
                case nameof(B2BCatalogFeed):
                    query.OfType<CatalogFeed, B2BCatalogFeed>();
                    break;
                case nameof(CloneCatalogFeed):
                    query.OfType<CatalogFeed, CloneCatalogFeed>();
                    break;
                case nameof(MALCatalogFeed):
                    query.OfType<CatalogFeed, MALCatalogFeed>();
                    break;
                default:
                    throw new BadRequestException("Invlid ObjectType");
            }
        }

        if (request.Criteria.TryGetUidValueFromEqCondition(Condition.LookupId, out var lookupId))
        {
            // hack for first load
            query.Eq(x => x.Id, lookupId);
        }

        if (request.Criteria.TryGetEqCondition(Condition.AutoComplete, out var condition) && !string.IsNullOrEmpty(condition.Value?.ToString()))
        {
            // autocomplete
            query.Regex(x => x.Name, new BsonRegularExpression($"{Regex.Escape(condition.Value.ToString())}", "i"));
        }

        var list = await query
            .IncludeField(x => x.Id)
            .IncludeField(x => x.Name)
            .IncludeField("_t")
            // .Project(x => new ReferenceValue { Id = x.Id.ToString(), Value = x.Name })
            // .ToListAsync();
            .FindAsync();

        var result =  list.Select(x => new ReferenceValue { Id = x.Id.ToString(), Value = x.Name });
        return result;
    }
}