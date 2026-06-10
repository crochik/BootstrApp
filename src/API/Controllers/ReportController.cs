using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers
{
    [Authorize("default")]
    [Route("/api/v1/[controller]")]
    public class ReportController : APIController
    {
        private readonly ILogger<ReportController> _logger;
        private readonly ReportService _reportsService;
        private readonly UserActionService _actionUserService;
        private readonly IOrganizationAdapter _organizationAdapter;

        public ReportController(
            ILogger<ReportController> logger,
            ReportService reportsService,
            UserActionService actionUserService,
            IOrganizationAdapter organizationAdapter
            )
        {
            _logger = logger;
            _reportsService = reportsService;
            _actionUserService = actionUserService;
            _organizationAdapter = organizationAdapter;
        }

        [Authorize("managerplus")]
        [HttpPost("/api/v1/[controller]({id})/DataView")]
        [ProducesResponseType(typeof(DataViewResponse), 200)]
        [Produces("text/csv", "application/json")]
        public async Task<IActionResult> RenderReportAsync([FromRoute] Guid id, [FromBody] DataViewRequest request)
        {
            Prepare(request);

            var result = await _reportsService.ReportAsync(Context, id, request);

            // TODO: add opt-in ObjectType field to report
            // ...

            return result == null ? NotFound() : Ok(result);
        }

        [Authorize("managerplus")]
        [HttpPost("{report}/DataView")]
        [ProducesResponseType(typeof(DataViewResponse), 200)]
        [Produces("text/csv", "application/json")]
        public async Task<IActionResult> RenderReportAsync([FromRoute] string report, [FromBody] DataViewRequest request)
        {
            Prepare(request);
            
            var result = await _reportsService.ReportAsync(Context, report, request);

            return result == null ? NotFound() : Ok(result);
        }
    }
}
