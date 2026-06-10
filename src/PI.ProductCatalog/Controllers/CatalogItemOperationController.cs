using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.ProductCatalog.Models;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;
using Crochik.Extensions;

namespace Controllers;

[Route("/productcatalog/v1/[controller]")]
public class CatalogItemOperationController : AbstractObjectTypeController<CatalogItemOperation>
{
    private readonly MongoConnection _connection;
    private string[] Except =
    [
        nameof(CatalogItem.AccountId),
        nameof(CatalogItem.CatalogFeedId),
        nameof(CatalogItem.CreatedOn),
        nameof(CatalogItem.Description),
        nameof(CatalogItem.EntityId),
        nameof(CatalogItem.Id),
        nameof(CatalogItem.IsFavorite),
        nameof(CatalogItem.IsHidden),
        nameof(CatalogItem.LastActor),
        nameof(CatalogItem.LastModifiedOn),
        nameof(CatalogItem.Margin),
        nameof(CatalogItem.ParentIds),
        nameof(CatalogItem.Tags),
        nameof(CatalogItem.UniqueColorCode),
        nameof(CatalogItem.UniqueStyleCode)
    ];

    public CatalogItemOperationController(
        MongoConnection connection,
        ObjectTypeService objectTypeService 
    ) : base(objectTypeService)
    {
        _connection = connection;
    }

    [Authorize("default")]
    [HttpPost("{objectType}/Lookup")]
    public async Task<IEnumerable<ReferenceValue>> LookupAsync([FromRoute] string objectType, DataViewRequest request)
    {
        await Task.CompletedTask;

        switch (objectType)
        {
            case "ChangedProperty":
                return ChangedPropertyNameLookup(request);

            case nameof(CatalogFeed):
                return await _connection.CatalogFeedLookupAsync(Context, request);

            default:
                throw new BadRequestException("Invalid Object for Lookup");
        }
    }

    private IEnumerable<ReferenceValue> ChangedPropertyNameLookup(DataViewRequest request)
    {
        if (request.Criteria.TryGetEqCondition(Condition.LookupId, out var condition))
        {
            // hack for first load
            return new ReferenceValue
            {
                Id = condition.Value.ToString(),
                Value = condition.Value.ToString()
            }.AsEnumerable();
        }

        return typeof(CatalogItem)
            .GetProperties()
            .Select(x => new ReferenceValue
            {
                Id = x.Name,
                Value = x.Name
            })
            .Where(x => !Except.Contains(x.Id))
            .OrderBy(x => x.Value);
    }
}