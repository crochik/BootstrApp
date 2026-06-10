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
[Route("/slack/v1")]
public class SettingsController : AbstractIntegrationController
{
    private const string ServiceName = "Slack";

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

    [Authorize("managerplus")]
    [HttpGet("[controller]")]
    [ProducesResponseType(typeof(SlackIntegrationData), 200)]
    public async Task<IActionResult> GetIntegrationAsync()
        => await GetEntityIntegrationDataAsync<SlackIntegrationData>(Context, ServiceName);

    [Authorize("managerplus")]
    [HttpGet("[controller]/Trunk")]
    [ProducesResponseType(typeof(IEnumerable<TrunkIntegrationData>), 200)]
    public async Task<IActionResult> GetTrunkIntegrationAsync()
        => await GetTrunkIntegrationDataAsync<SlackIntegrationData>(ServiceName);

    [Authorize("managerplus")]
    [HttpPut("Entity({id})/[controller]")]
    [ProducesResponseType(typeof(SlackIntegrationData), 200)]
    public async Task<IActionResult> AddIntegrationToEntityAsync(
        [FromBody] SlackIntegrationData body,
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

            return await AddAsync(id, body);
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

            return await AddAsync(id, body);
        }

        return NotFound();
    }

    [Authorize("managerplus")]
    [HttpPut("[controller]")]
    [ProducesResponseType(typeof(SlackIntegrationData), 200)]
    public async Task<IActionResult> AddIntegrationAsync([FromBody] SlackIntegrationData body)
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

        return await AddAsync(entityId, body);
    }

    [Authorize("managerplus")]
    [HttpDelete("[controller]")]
    public async Task<IActionResult> RemoveIntegrationAsync()
    {
        var result = await _entityIntegrationAdapter.DeleteAsync(Context, ServiceName);
        return result ? (IActionResult)Ok() : NotFound();
    }

    private async Task<IActionResult> AddAsync(Guid entityId, SlackIntegrationData body)
    {
        if (body == null || body.HookUrl == null)
        {
            return BadRequest();
        }

        var result = await AddIntegrationToEntityAsync(entityId, ServiceName, body);
        return result;
    }

    public class SlackIntegrationData : SlackIntegration.Data { }
    public class TrunkIntegrationData : IntegrationData<SlackIntegrationData> {}
}