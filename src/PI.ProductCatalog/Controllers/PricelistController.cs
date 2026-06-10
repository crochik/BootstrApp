// using System;
// using System.Threading.Tasks;
// using AutoMapper;
// using Crochik.Dipper;
// using Crochik.Mongo;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Logging;
// using MongoDB.Bson;
// using PI.ProductCatalog.Models;
// using PI.Shared.Controllers;
// using PI.Shared.Exceptions;
// using PI.Shared.Models;

// namespace Controllers
// {
//     [Route("/productcatalog/v1/[controller]")]
//     public partial class PricelistController : APIController
//     {
//         private readonly ILogger<PricelistController> _logger;
//         private readonly IMapper _mapper;
//         private readonly MongoConnection _connection;

//         private Guid EntityId => Context.Role switch
//         {
//             EntityRoleId.Admin => Context.AccountId.Value,
//             EntityRoleId.Manager => Context.OrganizationId.Value,
//             EntityRoleId.User => Context.OrganizationId.Value,
//             _ => throw new ForbiddenException(Context, "Invalid Context")
//         };

//         private Guid PricelistEntityId => Context.Role switch
//         {
//             EntityRoleId.Admin => Context.EntityId.Value, // for admins, maintain price list for user id
//             _ => EntityId,
//         };

//         public PricelistController(
//             ILogger<PricelistController> logger,
//             IMapper mapper,
//             MongoConnection connection
//             )
//         {
//             this._logger = logger;
//             this._mapper = mapper;
//             this._connection = connection;
//         }

//         private async Task<PricelistBreadcrumbView> LoadAsync(Guid id)
//         {
//             var breadcrumb = await _connection.Filter<Breadcrumb>()
//                 .Eq(x => x.AccountId, Context.AccountId.Value)
//                 .Eq(x => x.EntityId, EntityId)
//                 .Eq(x => x.Id, id)
//                 .FirstOrDefaultAsync();

//             if (breadcrumb == null) throw new NotFoundException(nameof(Breadcrumb), id);

//             var pricelist = await _connection.Filter<BreadcrumbPricing>()
//                 .Eq(x => x.AccountId, Context.AccountId.Value)
//                 .Eq(x => x.EntityId, PricelistEntityId)
//                 .Eq(x => x.ReferenceId, breadcrumb.Id)
//                 .FirstOrDefaultAsync();

//             return new PricelistBreadcrumbView(pricelist, breadcrumb);
//         }

//         [Authorize("managerplus")]
//         [HttpGet("/productcatalog/v1/[controller]({id})")]
//         public async Task<BreadcrumbPricing> GetAsync([FromRoute] Guid id)
//         {
//             var result = await LoadAsync(id);
//             if (result.Pricelist == null) throw new NotFoundException(nameof(BreadcrumbPricing), id);
//             return result.Pricelist;
//         }
//     }
// }