using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.ProductCatalog.Services;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers
{
    [Route("/productcatalog/v1/[controller]")]
    public class ProductCatalogController : APIController
    {
        private readonly ILogger<ProductCatalogController> _logger;
        private readonly MongoConnection _connection;
        private readonly CatalogService _catalogService;
        private readonly ObjectTypeService _objectTypeService;
        private readonly UserActionService _userActionService;

        public ProductCatalogController(
            ILogger<ProductCatalogController> logger,
            MongoConnection connection,
            CatalogService catalogService,
            ObjectTypeService objectTypeService,
            UserActionService userActionService
            )
        {
            this._logger = logger;
            this._connection = connection;
            this._catalogService = catalogService;
            this._objectTypeService = objectTypeService;
            this._userActionService = userActionService;
        }

        // [Authorize("admin")]
        // [HttpGet("DataForm")]
        // public async Task<PI.Shared.Form.Models.Form> GetEditFormAsync([FromQuery] Guid? id)
        // {
        //     var objectType = typeof(ProductCatalog).Name;
        //     var form = await _objectTypeService.GetDataFormAsync(Context, objectType, id);
        //     if (form == null) throw new NotFoundException(objectType, id);
        //     return form;
        // }

        [Authorize("admin")]
        [HttpPost("DataForm")]
        public async Task<DataFormActionResponse> EditFormOnActionAsync([FromBody] DataFormActionRequest request)
        {
            try
            {
                return request.Action switch
                {
                    FormAction.Add => await AddCatalogAsync(request),
                    _ => await _objectTypeService.ExecObjectActionAsync<ProductCatalog>(Context, request)
                };
            }
            catch (Exception ex)
            {
                return new DataFormActionResponse(request, ex.Message);
            }
        }

        /// <summary>
        /// PlProductSettings Actions 
        /// </summary>
        [Authorize("manager")]
        [HttpGet("PlProductSettings/{command}/DataForm")]
        public PI.Shared.Form.Models.Form GetProductSettingsDataForm([FromRoute] string command, [FromQuery] Guid? id)
        {
            if (command != "Upload") throw new BadRequestException("Invalid Action");

            return new PI.Shared.Form.Models.Form
            {
                Name = "Upload",
                Fields = new PI.Shared.Form.Models.FormField[]
                {
                    new PI.Shared.Form.Models.FileField
                    {
                        Name = "File",
                    },
                },
                Actions = new PI.Shared.Form.Models.FormAction[]
                {
                    new PI.Shared.Form.Models.FormAction
                    {
                        Name = "Upload",
                        Action = "Upload"
                    }
                }
            };
        }

        [Authorize("manager")]
        [HttpPost("PlProductSettings/{command}/DataForm")]
        [Consumes("application/octet-stream", "multipart/form-data")]
        public async Task<DataFormActionResponse> DataViewImportByIdAsync([FromRoute] string command, IFormFile file)
        {
            if (command != "Upload") throw new BadRequestException("invalid action");

            var objectTypeName = "PlProductSettings";
            var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
            if (objectType == null) throw new NotFoundException($"{objectTypeName} not found");

            var result = await _objectTypeService.ImportCsvAsync(Context, objectType, file.OpenReadStream());

            if (result.Success)
            {
                var catalog = await _connection.Filter<ProductCatalog>()
                    .Eq(x => x.AccountId, Context.AccountId.Value)
                    .Eq(x => x.EntityId, Context.OrganizationId.Value)
                    .FirstOrDefaultAsync();

                if (catalog != null)
                {
                    _ = Task.Run(() => _catalogService.ResetAllMarginsAsync(catalog));
                }
            }

            return result;
        }

        private async Task<DataFormActionResponse> AddCatalogAsync(DataFormActionRequest request)
        {
            if (!request.TryGetGuidParam(nameof(ProductCatalog.EntityId), out var entityId))
            {
                return new DataFormActionResponse(request, "Missing required EntityId");
            }

            var existing = await _connection.Filter<ProductCatalog>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.EntityId, entityId)
                .FirstOrDefaultAsync();

            if (existing != null) return new DataFormActionResponse(request, "Only one catalog per Entity");

            var org = await _connection.Filter<Entity, Organization>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Id, entityId)
                .FirstOrDefaultAsync();

            if (org == null) return new DataFormActionResponse(request, "Organization not found");

            var resp = await _objectTypeService.ExecObjectActionAsync<ProductCatalog>(Context, request);

            return resp;
        }

        [Authorize("admin")]
        [HttpPost("/productcatalog/v1/Entity({entityId})/[controller]/ResetBreadcrumbs")]
        public async Task<IActionResult> ResetBreadcrumbs([FromRoute] Guid entityId)
        {
            var catalog = await _connection.Filter<ProductCatalog>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.EntityId, entityId)
                .FirstOrDefaultAsync();

            if (catalog==null) throw new NotFoundException("No catalog for entity");

            _ = Task.Run(() => _catalogService.ResetBreadcrumbsAsync(Context, entityId));

            return Ok("Reset started");
        }
    }
}
