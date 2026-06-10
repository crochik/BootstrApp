using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace Controllers
{
    [Authorize("default")]
    [Produces("application/json")]
    [Route("/api/v1/Entity/Integration")]
    public class EntityIntegrationController : APIController
    {
        private readonly IIntegrationAdapter _integrationAdapter;
        private readonly IEntityIntegrationAdapter _entityIntegrationAdapter;

        public EntityIntegrationController
            (
                IIntegrationAdapter integrationAdapter,
                IEntityIntegrationAdapter entityIntegrationAdapter
            )
        {
            this._integrationAdapter = integrationAdapter;
            this._entityIntegrationAdapter = entityIntegrationAdapter;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<EntityIntegration>), 200)]
        public async Task<IActionResult> GetForUserAsync()
        {
            var list = await _entityIntegrationAdapter.GetForUserAsync(Context);
            return Ok(list);
        }
    }
}