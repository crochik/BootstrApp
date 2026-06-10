using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.ProductCatalog.Services;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Requests;

namespace Controllers;

[Route("/productcatalog/v1/[controller]")]
public class ItemController : AbstractItemController
{
    private readonly CatalogService _catalogService;

    public ItemController(
        ILogger<ItemController> logger,
        IMapper mapper,
        MongoConnection connection,
        CatalogService catalogService
    ) : base(logger, mapper, connection)
    {
        _catalogService = catalogService;
    }

    [Authorize("managerplus")]
    [HttpPatch("/productcatalog/v1/[controller]({parentId})")]
    public async Task<Breadcrumb> UpdateChildrenAsync([FromRoute] Guid parentId, [FromBody] DataViewRequest request, decimal? margin, bool? isFavorite, bool? isHidden)
        => await UpdateChildrenAsync(EntityId, parentId, request, margin, isFavorite, isHidden);

    private async Task<Breadcrumb> UpdateChildrenAsync(Guid entityId, Guid parentId, DataViewRequest request, decimal? margin, bool? isFavorite, bool? isHidden)
    {
        var pricelist = await LoadAsync(entityId, parentId);
        var modified = false;

        var query = _connection.Filter<Breadcrumb>()
            .Eq(x => x.Id, pricelist.Id)
            .Update
            .Set(x => x.LastActor, Context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow);

        if (margin.HasValue && (!pricelist.Margin.HasValue || margin.Value != pricelist.Margin.Value))
        {
            query.Set(x => x.Margin, margin.Value);
            modified = true;
        }

        if (isFavorite.HasValue && pricelist.IsFavorite != isFavorite.Value)
        {
            if (isFavorite.Value)
            {
                query.AddToSet(x => x.Tags, AbstractCatalogEntity.FAVORITE_TAG);
            }
            else
            {
                query.Pull(x => x.Tags, AbstractCatalogEntity.FAVORITE_TAG);
            }

            modified = true;
        }

        if (isHidden.HasValue && pricelist.IsHidden != isHidden.Value)
        {
            query.Set(x => x.IsHidden, isHidden.Value);
            modified = true;
        }

        if (modified)
        {
            pricelist = await query.UpdateAndGetOneAsync();
        }

        if (margin.HasValue)
        {
            // cascade "margin" to children
            var update = _connection.Filter<Breadcrumb>()
                .Eq(x => x.AccountId, pricelist.AccountId)
                .Eq(x => x.EntityId, entityId)
                .AnyEq(x => x.ParentIds, pricelist.Id)
                .Update
                .Set(x => x.LastActor, Context.Actor())
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.Margin, margin.Value);

            await update.UpdateManyAsync();

            // // TODO: delete the children that match exactly
            // var delete = _connection.Filter<Breadcrumb>()
            //     .Eq(x => x.AccountId, pricelist.AccountId)
            //     .Eq(x => x.EntityId, PricelistEntityId)
            //     .AnyEq(x => x.ParentIds, pricelist.Id)
            //     .Eq(x => x.Margin, pricelist.Margin)
            //     .Eq(x => x.Tags, pricelist.Tags)
            //     .Eq(x => x.IsHidden, pricelist.IsHidden)
            //     .DeleteAsync();
        }

        // // TODO: add items 
        // await _connection.DipperAggregateAsync("AddItems", "productCatalog", new {
        //     AccountId = Context.AccountId.ToString(),
        //     EntityId = PricelistEntityId.ToString(),
        //     ParentId = pricelist.Id.ToObjectId(),
        // });

        // TODO: update items
        if (margin.HasValue)
        {
            // cascade "margin" to items
            var update = _connection.Filter<CatalogItem>()
                .Eq(x => x.AccountId, pricelist.AccountId)
                .Eq(x => x.EntityId, entityId)
                .AnyEq(x => x.ParentIds, pricelist.Id)
                .Update
                .Set(x => x.LastActor, Context.Actor())
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.Margin, margin.Value);

            await update.UpdateManyAsync();
        }

        var feed = await _connection.Filter<B2BCatalogFeed>()
            .Eq(x => x.Id, pricelist.CatalogFeedId)
            .FirstOrDefaultAsync();

        await _catalogService.SetLastUpdatedOnAsync(Context, feed);

        return pricelist;
    }


    [Authorize("managerplus")]
    [HttpPost("DataView")]
    [ProducesResponseType(typeof(DataViewResponse), 200)]
    [Produces("text/csv", "application/json")]
    public async Task<DataViewResponse> DataViewAsync([FromBody] DataViewRequest request)
        => await DataViewAsync(EntityId, request, "productcatalog/v1/Item");


    [Authorize("admin")]
    [HttpPost("/productcatalog/v1/Entity({entityId})/[controller]/DataView")]
    [ProducesResponseType(typeof(DataViewResponse), 200)]
    [Produces("text/csv", "application/json")]
    public async Task<DataViewResponse> AdminDataViewAsync([FromRoute] Guid entityId, [FromBody] DataViewRequest request)
        => await DataViewAsync(entityId, request, $"productcatalog/v1/Entity({entityId})/Item");

    private async Task<DataViewResponse> DataViewAsync(Guid entityId, DataViewRequest request, string prefix)
    {
        Prepare(request);

        var response = new DataViewResponse
        {
            Request = request,
            View = GetDataView(request, false, prefix),
        };

        response.Result = await GetResultAsync<CatalogItem>(entityId, response);

        return response.UpdateFields();
    }

    [Authorize("managerplus")]
    [HttpPost("/productcatalog/v1/[controller]({parentId})/DataView")]
    [Produces("text/csv", "application/json")]
    public async Task<DataViewResponse> ChildDataViewAsync([FromRoute] Guid parentId, [FromBody] DataViewRequest request)
        => await ChildDataViewAsync(EntityId, parentId, request, "productcatalog/v1/Item");

    [Authorize("admin")]
    [HttpPost("/productcatalog/v1/Entity({entityId})/[controller]({parentId})/DataView")]
    [Produces("text/csv", "application/json")]
    public async Task<DataViewResponse> AdminChildDataViewAsync([FromRoute] Guid entityId, [FromRoute] Guid parentId, [FromBody] DataViewRequest request)
        => await ChildDataViewAsync(entityId, parentId, request, $"productcatalog/v1/Entity({entityId})/Item");

    private async Task<DataViewResponse> ChildDataViewAsync(Guid entityId, Guid parentId, DataViewRequest request, string prefix)
    {
        Prepare(request);

        var parent = await LoadAsync(entityId, parentId);

        request ??= new DataViewRequest
        {
        };

        if (request.Criteria.TryGetEqCondition(nameof(Breadcrumb.ParentIds), out var filter))
        {
            filter.Value = parentId;
        }
        else
        {
            filter = new Condition
            {
                FieldName = nameof(Breadcrumb.ParentIds),
                Operator = Operator.Eq,
                Value = parentId
            };

            request.Criteria = request.Criteria == null ? new[] { filter } : request.Criteria.Append(filter).ToArray();
        }

        var response = new DataViewResponse
        {
            Request = request,
            View = GetDataView(request, true, prefix),
        };

        response.Result = await GetResultAsync<CatalogItem>(entityId, response);
        response.View.Title = parent.Name;

        return response.UpdateFields();
    }

    [Authorize("default")]
    [HttpGet("/productcatalog/v1/[controller]({id})/DataForm")]
    public async Task<Form> GetEditFormAsync([FromRoute] Guid id)
        => await GetEditFormAsync<CatalogItem>(EntityId, id);

    [Authorize("admin")]
    [HttpGet("/productcatalog/v1/Entity({entityId})/[controller]({id})/DataForm")]
    public async Task<Form> AdminGetEditFormAsync([FromRoute] Guid entityId, [FromRoute] Guid id)
        => await GetEditFormAsync<CatalogItem>(entityId, id);

    [Authorize("default")]
    [HttpPost("/productcatalog/v1/[controller]({id})/DataForm")]
    public async Task<DataFormActionResponse> EditFormOnActionAsync([FromRoute] Guid id, [FromBody] DataFormActionRequest request)
    {
        // var result = await _objectTypeService.ExecObjectActionAsync(Context, objectType, request);
        // if (result == null) throw new NotFoundException();

        // return result;

        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    [Authorize("default")]
    [HttpPost("{objectType}/Lookup")]
    public async Task<IEnumerable<BreadcrumbReferenceValue>> LookupAsync([FromRoute] string objectType, DataViewRequest request)
        => await LookupAsync<CatalogItem>(EntityId, objectType, request);


    // [Authorize("admin")]
    // [HttpPost("Upgrade")]
    // public async Task<IActionResult> UpgradeAsync()
    // {
    //     await Task.CompletedTask;
    //     return Ok();
    // }
}