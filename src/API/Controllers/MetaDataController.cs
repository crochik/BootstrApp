using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;

namespace Controllers
{
    [Route("/api/v1/[controller]")]
    public class MetaDataController : APIController
    {
        private readonly ILogger<MetaDataController> _logger;
        private readonly IEntityMetadataAdapter _adapter;
        private readonly IOrganizationAdapter _organizationAdapter;

        public MetaDataController(
            ILogger<MetaDataController> logger,
            IEntityMetadataAdapter adapter,
            IOrganizationAdapter organizationAdapter
            )
        {
            this._logger = logger;
            this._adapter = adapter;
            this._organizationAdapter = organizationAdapter;
        }

        [HttpGet]
        [Authorize("default")]
        [ProducesResponseType(typeof(IEnumerable<IEntityMetadata>), 200)]
        public async Task<IActionResult> GetAllAsync()
        {
            var list = await _adapter.GetAsync(Context);
            return Ok(list);
        }

        [HttpGet("/api/v1/Organization({id})/[controller]")]
        [Authorize("managerplus")]
        [ProducesResponseType(typeof(IEnumerable<IEntityMetadata>), 200)]
        public async Task<IActionResult> GetByOrganizationAsync([FromRoute] Guid id)
        {
            var list = await _adapter.GetForEntityAsync(id);
            return Ok(list);
        }

        [HttpDelete("/api/v1/Organization({id})/[controller]")]
        [Authorize("managerplus")]
        [ProducesResponseType(typeof(IEnumerable<IEntityMetadata>), 200)]
        public async Task<IActionResult> DeleteFromOrganizationAsync([FromRoute] Guid id, [FromBody] EntityMetadata[] list)
        {
            if (list == null || list.Length < 1)
            {
                return BadRequest();
            }

            var org = await _organizationAdapter.GetByIdAsync(id);
            if (org == null)
            {
                return NotFound();
            }

            if (org.AccountId != Context.AccountId.Value)
            {
                return Forbid();
            }

            foreach (var row in list)
            {
                if ( row.PartitionId!=org.AccountId || row.EntityId!=id)
                {
                    return Forbid();
                }
            }

            var result = await _adapter.DeleteAsync(list);

            return Ok(result);            
        }

        [HttpPost("/api/v1/Organization({id})/[controller]")]
        [Authorize("managerplus")]
        [ProducesResponseType(typeof(IEnumerable<IEntityMetadata>), 200)]
        public async Task<IActionResult> UpsertToOrganizationAsync([FromRoute] Guid id, [FromBody] EntityMetadata[] list)
        {
            if (list == null || list.Length < 1)
            {
                return BadRequest();
            }

            var org = await _organizationAdapter.GetByIdAsync(id);
            if (org == null)
            {
                return NotFound();
            }

            if (org.AccountId != Context.AccountId.Value)
            {
                return Forbid();
            }

            foreach (var row in list)
            {
                row.PartitionId = Context.AccountId.Value;
                row.EntityId = id;
            }

            var result = await _adapter.AddForEntityAsync(id, list);

            return Ok(result);
        }
    }
}
