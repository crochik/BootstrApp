using System;
using System.Threading.Tasks;
using Controllers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Services;

namespace Controllers
{
    /// <summary>
    /// Update integrations associated with lead
    /// </summary>
    [Produces("application/json")]
    [Route("/api/v1/[controller]")]
    [Authorize(default)]
    public class LeadIntegrationController : APIController
    {
        private readonly ILogger<LeadIntegrationController> _logger;
        private readonly ILeadAdapter _leadAdapter;
        private readonly IIntegrationLeadAdapter _integrationLeadAdapter;
        private readonly LeadBuilderService _leadBuilderService;

        public LeadIntegrationController(
            ILogger<LeadIntegrationController> logger,
            ILeadAdapter leadAdapter,
            IIntegrationLeadAdapter integrationLeadAdapter,
            LeadBuilderService leadBuilderService
            )
        {
            this._logger = logger;
            this._leadAdapter = leadAdapter;
            this._integrationLeadAdapter = integrationLeadAdapter;
            this._leadBuilderService = leadBuilderService;
        }

        [HttpPost("/api/v1/Lead({id})/Integration({integrationId})/event")]
        [ProducesResponseType(typeof(IntegrationEvent), 200)]
        public Task<IActionResult> UpdateIntegrationAsync([FromRoute] Guid id, [FromRoute] Guid integrationId, [FromBody] IntegrationEvent evt)
        {
            // var lead = await _leadAdapter.GetByIdAsync(id);
            // if (lead == null) return NotFound();

            // var integrations = await _integrationLeadAdapter.GetAsync(id);
            // var integration = integrations.FirstOrDefault(x => x.IntegrationId == integrationId);
            // if (integration == null) return NotFound();

            // // TODO: enforce access rules?
            // // ...

            // return Ok(evt);
            throw new NotImplementedException();
        }
    }
}