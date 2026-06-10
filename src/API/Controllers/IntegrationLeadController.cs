using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Controllers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;

namespace Controllers
{
    /// <summary>
    /// Called by the integration(s) to manage Leads
    /// </summary>
    [Produces("application/json")]
    [Route("/api/v1/[controller]")]
    public class IntegrationLeadController : APIController
    {
        private readonly IMapper _mapper;
        private readonly IIntegrationLeadAdapter _integrationLeadAdapter;

        public IntegrationLeadController(
            IMapper mapper,
            IIntegrationLeadAdapter integrationLeadAdapter
            )
        {
            this._mapper = mapper;
            this._integrationLeadAdapter = integrationLeadAdapter;
        }

        [Authorize("default")]
        [HttpGet("/api/v1/Lead({id})/Integration")]
        [ProducesResponseType(typeof(LeadIntegration[]), 200)]
        public async Task<IActionResult> GetIntegrationsAsync(Guid id)
        {
            // TODO: enforce user has access to leadtypeid
            // ...

            // if user, limit leads to entityid; 
            // if manager, limit leads to users of branch

            var integrations = await _integrationLeadAdapter.GetAsync(id);
            var result = integrations == null ?
                (IEnumerable<LeadIntegration>)Array.Empty<LeadIntegration>() :
                integrations.ToList().ConvertAll(i => _mapper.Map<LeadIntegration>(i));

            return Ok(result);
        }
    }
}