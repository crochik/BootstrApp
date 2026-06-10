using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.ProductCatalog.Models;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Route("/productcatalog/v1/[controller]")]
public partial class CatalogSyncJobController : AbstractObjectTypeController<CatalogSyncJob>
{
    private readonly MongoConnection _connection;

    public CatalogSyncJobController(
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
        switch (objectType)
        {
            case nameof(CatalogFeed):
                return await _connection.CatalogFeedLookupAsync(Context, request);

            default:
                throw new BadRequestException("Invalid Object for Lookup");
        }
    }
}