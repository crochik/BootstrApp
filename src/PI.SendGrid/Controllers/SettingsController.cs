using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace Controllers;

[Authorize("default")]
[Produces("application/json")]
[Route("/sendgrid/v1/[controller]")]
public class SettingsController : AbstractIntegrationController
{
    private const string ServiceName = "SendGrid";

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

    [Authorize("admin")]
    [HttpGet("/sendgrid/v1/Account/[controller]")]
    [ProducesResponseType(typeof(IntegrationData), 200)]
    public async Task<IActionResult> GetAccountIntegrationAsync()
        => await GetEntityIntegrationDataAsync<IntegrationData>(Context, ServiceName);

    [Authorize("managerplus")]
    [HttpGet]
    [ProducesResponseType(typeof(IntegrationData), 200)]
    public async Task<IActionResult> GetIntegrationAsync()
        => await GetEntityIntegrationDataAsync<IntegrationData>(Context, ServiceName);

    [Authorize("managerplus")]
    [HttpGet("Trunk")]
    [ProducesResponseType(typeof(IEnumerable<TrunkIntegrationData>), 200)]
    public async Task<IActionResult> GetTrunkIntegrationAsync()
        => await GetTrunkIntegrationDataAsync<IntegrationData>(ServiceName);

    [Authorize("managerplus")]
    [HttpPut("/sendgrid/v1/Entity({id})/[controller]")]
    [ProducesResponseType(typeof(IntegrationData), 200)]
    public async Task<IActionResult> AddIntegrationToEntityAsync(
        [FromBody] IntegrationRequest request,
        [FromRoute] Guid id,
        [FromServices] IOrganizationAdapter organizationAdapter,
        [FromServices] IUserAdapter userAdapter)
    {
        // TODO: use context and canAccess
        // ...
        var user = await userAdapter.GetByIdAsync(id);
        if (user != null)
        {
            switch (Context.Role)
            {
                case EntityRoleId.Admin:
                    if (user.AccountId != Context.AccountId.Value) return Forbid();
                    break;

                case EntityRoleId.Manager:
                    if (user.OrganizationId != Context.OrganizationId.Value) return Forbid();
                    break;

                default:
                    return Forbid();
            }

            return await AddAsync(id, request);
        }

        var org = await organizationAdapter.GetByIdAsync(id);
        if (org != null)
        {
            switch (Context.Role)
            {
                case EntityRoleId.Admin:
                    if (org.AccountId != Context.AccountId.Value) return Forbid();
                    break;

                case EntityRoleId.Manager:
                    if (org.Id != Context.OrganizationId.Value) return Forbid();
                    break;

                default:
                    return Forbid();
            }

            return await AddAsync(id, request);
        }

        return NotFound();
    }

    [Authorize("managerplus")]
    [HttpPut]
    [ProducesResponseType(typeof(IntegrationData), 200)]
    public async Task<IActionResult> AddIntegrationAsync([FromBody] IntegrationRequest request)
    {
        Guid entityId;
        switch (Context.Role)
        {
            case EntityRoleId.Admin:
            case EntityRoleId.Account:
                entityId = Context.AccountId.Value;
                break;

            case EntityRoleId.Manager:
            case EntityRoleId.Organization:
                entityId = Context.OrganizationId.Value;
                break;

            default:
                return Forbid();
        }

        return await AddAsync(entityId, request);
    }

    [Authorize("managerplus")]
    [HttpDelete]
    public async Task<IActionResult> RemoveIntegrationAsync()
    {
        var result = await _entityIntegrationAdapter.DeleteAsync(Context, ServiceName);
        return result ? (IActionResult)Ok() : NotFound();
    }

    private async Task<IActionResult> AddAsync(Guid entityId, IntegrationRequest request)
    {
        // TODO: validate 
        // ...

        var result = await AddIntegrationToEntityAsync(entityId, ServiceName, request.Data, request.Auth);
        return result;
    }

    public class IntegrationRequest
    {
        public IntegrationData Data { get; set; }
        public IntegrationAuth Auth { get; set; }
    }

    public class IntegrationAuth : SendGridIntegration.Authentication { }
    public class IntegrationData : SendGridIntegration.Data { }
    public class TrunkIntegrationData : IntegrationData<IntegrationData> { }
}