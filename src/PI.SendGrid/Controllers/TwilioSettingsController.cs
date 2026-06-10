using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace Controllers;

[Authorize("default")]
[Produces("application/json")]
[Route("/twilio/v1/Settings")]
public class TwilioSettingsController : AbstractIntegrationController
{
    private const string ServiceName = nameof(IntegrationIds.Twilio);

    public TwilioSettingsController(
        ILogger<TwilioSettingsController> logger,
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
    [HttpGet("/twilio/v1/Account/Settings")]
    [ProducesResponseType(typeof(TwilioIntegrationData), 200)]
    public async Task<IActionResult> GetAccountIntegrationAsync()
        => await GetEntityIntegrationDataAsync<TwilioIntegrationData>(Context, ServiceName);

    [Authorize("managerplus")]
    [HttpGet]
    [ProducesResponseType(typeof(TwilioIntegrationData), 200)]
    public async Task<IActionResult> GetIntegrationAsync()
        => await GetEntityIntegrationDataAsync<TwilioIntegrationData>(Context, ServiceName);

    [Authorize("managerplus")]
    [HttpGet("Trunk")]
    [ProducesResponseType(typeof(IEnumerable<TwilioTrunkIntegrationData>), 200)]
    public async Task<IActionResult> GetTrunkIntegrationAsync()
        => await GetTrunkIntegrationDataAsync<TwilioIntegrationData>(ServiceName);

    [Authorize("managerplus")]
    [HttpPut("/twilio/v1/Entity({id})/Settings")]
    [ProducesResponseType(typeof(TwilioIntegrationData), 200)]
    public async Task<IActionResult> AddIntegrationToEntityAsync(
        [FromBody] TwilioIntegrationRequest request,
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
    [ProducesResponseType(typeof(TwilioIntegrationData), 200)]
    public async Task<IActionResult> AddIntegrationAsync([FromBody] TwilioIntegrationRequest request)
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

    private async Task<IActionResult> AddAsync(Guid entityId, TwilioIntegrationRequest request)
    {
        // TODO: validate 
        // ...

        var result = await AddIntegrationToEntityAsync(entityId, ServiceName, request.Data, request.Auth);
        return result;
    }

    public class TwilioIntegrationRequest
    {
        public TwilioIntegrationData Data { get; set; }
        public TwilioIntegrationAuth Auth { get; set; }
    }

    public class TwilioIntegrationAuth : TwilioIntegration.Authentication { }
    public class TwilioIntegrationData : TwilioIntegration.Data { }
    public class TwilioTrunkIntegrationData : IntegrationData<TwilioIntegrationData> { }
}