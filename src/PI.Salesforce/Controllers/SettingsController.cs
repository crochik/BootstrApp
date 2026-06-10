using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;

namespace Controllers;

[Authorize("admin")]
[Produces("application/json")]
[Route("/salesforce/v1")]
public class SettingsController : AbstractIntegrationController
{
    private const string ServiceName = "Salesforce";

    public SettingsController(
        ILogger<SettingsController> logger,
        IIntegrationAdapter integrationAdapter,
        ILeadTypeAdapter leadTypeAdapter,
        IEntityIntegrationAdapter entityIntegrationAdapter,
        ILeadTypeIntegrationAdapter leadTypeIntegrationAdapter,
        IAppointmentTypeAdapter appointmentTypeAdapter,
        IAppointmentTypeIntegrationAdapter appointmentTypeIntegrationAdapter
    ) : base(
        logger,
        integrationAdapter,
        leadTypeAdapter,
        entityIntegrationAdapter,
        leadTypeIntegrationAdapter,
        appointmentTypeAdapter,
        appointmentTypeIntegrationAdapter            
    )
    {
    }

    [HttpGet("[controller]")]
    [ProducesResponseType(typeof(IntegrationData), 200)]
    public async Task<IActionResult> GetIntegrationAsync()
        => await GetEntityIntegrationDataAsync<IntegrationData>(Context, ServiceName);


    [HttpPut("[controller]")]
    [ProducesResponseType(typeof(IntegrationData), 200)]
    public async Task<IActionResult> AddIntegrationAsync([FromBody] IntegrationRequest body)
    {
        var entityId = Context.AccountId.Value;

        // always override for now
        body = new IntegrationRequest
        {
            Data = new IntegrationData
            {
                OverrideEntityId = Context.UserId.Value,
            },
            Auth = new IntegrationAuth
            {
            }
        };

        return await AddIntegrationToEntityAsync(entityId, ServiceName, body.Data, body.Auth);
    }

    [HttpDelete("[controller]")]
    public async Task<IActionResult> RemoveIntegrationAsync()
    {
        var result = await _entityIntegrationAdapter.DeleteAsync(Context, ServiceName);
        return result ? (IActionResult)Ok() : NotFound();
    }

    public class IntegrationRequest
    {
        public IntegrationData Data { get; set; }
        public IntegrationAuth Auth { get; set; }
    }

    public class IntegrationData : SalesforceIntegration.Data { }
    public class IntegrationAuth : SalesforceIntegration.Authentication {}
}