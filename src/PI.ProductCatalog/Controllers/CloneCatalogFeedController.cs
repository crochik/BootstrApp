using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.ProductCatalog.Models;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Route("/productcatalog/v1/CatalogFeed/clone")]
public class CloneCatalogFeedController : AbstractCatalogFeedController<CloneCatalogFeed>
{
    private readonly MongoConnection _connection;

    public CloneCatalogFeedController(
        ObjectTypeService objectTypeService,
        MongoConnection connection
    ) : base(objectTypeService)
    {
        _connection = connection;
    }

    [Authorize("default")]
    [HttpPost("DataForm")]
    public async Task<DataFormActionResponse> EditFormOnActionAsync([FromBody] DataFormActionRequest request)
    {
        return request.Action switch
        {
            FormAction.Add => await AddFeedAsync(request),
            _ => await OnActionAsync(request),
        };
    }

    private async Task<DataFormActionResponse> AddFeedAsync(DataFormActionRequest request)
    {
        Guid entityId;

        switch (Context.Role)
        {
            case EntityRoleId.Admin:
                if (!request.TryGetGuidParam(nameof(CloneCatalogFeed.EntityId), out entityId))
                {
                    return new DataFormActionResponse(request, "Missing required EntityId parameter");
                }
                break;

            case EntityRoleId.Manager:
                entityId = Context.OrganizationId.Value;
                request.Parameters[nameof(CloneCatalogFeed.EntityId)] = entityId;
                break;

            default:
                throw new ForbiddenException(Context, "Can't add Catalog Feed");
        }

        // validate catalogfeed source
        if (!request.TryGetGuidParam(nameof(CloneCatalogFeed.CatalogFeedId), out var catalogFeedId))
        {
            return new DataFormActionResponse(request, "Missing required CatalogFeedId parameter");
        }

        var catalogFeed = await _connection.Filter<CatalogFeed>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, Context.AccountId.Value)
            .Eq(x => x.Id, catalogFeedId)
            .FirstOrDefaultAsync();
        if (catalogFeed == null) return new DataFormActionResponse(request, "Invalid Catalog Feed");

        return await OnActionAsync(request);
    }
}