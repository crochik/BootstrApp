using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace PI.Shared.Controllers;

public abstract class AbstractIntegrationController : APIController
{
    private readonly ILogger<AbstractIntegrationController> _logger;
    private readonly ILeadTypeAdapter _leadTypeAdapter;
    private readonly IAppointmentTypeAdapter _appointmentTypeAdapter;
    protected readonly IIntegrationAdapter _integrationAdapter;
    protected readonly IEntityIntegrationAdapter _entityIntegrationAdapter;
    protected readonly ILeadTypeIntegrationAdapter _leadTypeIntegrationAdapter;
    protected readonly IAppointmentTypeIntegrationAdapter _appointmentTypeIntegrationAdapter;

    protected AbstractIntegrationController(
        ILogger<AbstractIntegrationController> logger,
        IIntegrationAdapter integrationAdapter,
        ILeadTypeAdapter leadTypeAdapter,
        IEntityIntegrationAdapter entityIntegrationAdapter,
        ILeadTypeIntegrationAdapter leadTypeIntegrationAdapter,
        IAppointmentTypeAdapter appointmentTypeAdapter,
        IAppointmentTypeIntegrationAdapter appointmentTypeIntegrationAdapter
    )
    {
        _logger = logger;
        _integrationAdapter = integrationAdapter;
        _leadTypeAdapter = leadTypeAdapter;
        _entityIntegrationAdapter = entityIntegrationAdapter;
        _leadTypeIntegrationAdapter = leadTypeIntegrationAdapter;
        _appointmentTypeAdapter = appointmentTypeAdapter;
        _appointmentTypeIntegrationAdapter = appointmentTypeIntegrationAdapter;
    }

    protected async Task<IActionResult> GetAppointmentTypeIntegrationDataAsync(Guid id, string serviceName)
    {
        // TODO: enforce access rules
        // ...

        var integration = await _integrationAdapter.GetByServiceNameAsync(serviceName);
        if (integration == null) return NotFound();

        var result = await _appointmentTypeIntegrationAdapter.GetByIdAsync(id, integration.Id);
        if (result == null) return NotFound();

        return Content(result.Data, "application/json");
    }

    protected async Task<IActionResult> GetEntityeIntegrationDataAsync(Guid entityId, string serviceName)
    {
        var integration = await _integrationAdapter.GetByServiceNameAsync(serviceName);
        if (integration == null) return NotFound();

        var result = await _entityIntegrationAdapter.FindForEntityAsync(entityId, integration.Id);
        if (result == null) return NotFound();

        return Content(result.Data, "application/json");
    }

    protected async Task<IActionResult> GetIntegrationDataAsync<T>(string serviceName)
        where T : class
    {
        var integration = _integrationAdapter.GetByServiceName(serviceName);
        if (integration == null) return NotFound();

        var entityId = Context?.Role switch
        {
            EntityRoleId.Admin => Context.AccountId.Value,
            EntityRoleId.Manager => Context.OrganizationId.Value,
            _ => (Guid?)null,
        };

        if (!entityId.HasValue) return Forbid();

        var result = await _entityIntegrationAdapter.FindForEntityAsync(entityId.Value, integration.Id);
        if (result == null) return NotFound();

        var data = result.GetData<T>();
        return Ok(data);
    }

    protected async Task<IActionResult> GetEntityIntegrationDataAsync<T>(IEntityContext context, string serviceName)
        where T : class
    {
        var integration = _integrationAdapter.GetByServiceName(serviceName);
        if (integration == null) return NotFound();

        var entityId = context.Role switch
        {
            EntityRoleId.Admin => Context.AccountId,
            EntityRoleId.Manager => Context.OrganizationId,
            _ => null
        };

        if (!entityId.HasValue) return Forbid();

        var result = await _entityIntegrationAdapter.FindForEntityAsync(entityId.Value, integration.Id);
        return result == null ? NotFound() : Ok(result.GetData<T>());
    }

    protected async Task<IActionResult> GetTrunkIntegrationDataAsync<T>(string serviceName)
        where T : class
    {
        var integration = _integrationAdapter.GetByServiceName(serviceName);
        if (integration == null) return NotFound();

        Guid entityId;
        switch (Context?.Role)
        {
            case EntityRoleId.Admin:
            case EntityRoleId.Manager:
            case EntityRoleId.User:
                entityId = Context.UserId.Value;
                break;

            default:
                return Forbid();
        }

        var result = await _entityIntegrationAdapter.GetTrunkByIdAsync(entityId, integration.Id);
        if (result == null) return NotFound();

        var records = result.Select(x => new IntegrationData<T>(x));
        return Ok(records);
    }

    protected async Task<IActionResult> GetLeadTypeIntegrationAsync(Guid id, string serviceName)
    {
        return await WithLeadTypeAsync(_leadTypeAdapter, id, async leadType =>
        {
            var integration = await _integrationAdapter.GetByServiceNameAsync(serviceName);
            if (integration == null) return NotFound();

            var result = await _leadTypeIntegrationAdapter.GetByIdAsync(id, integration.Id);
            if (result == null) return NotFound();

            return Content(result.Data, "application/json");
        });
    }

    protected async Task<IActionResult> AddIntegrationToLeadTypeAsync(Guid id, string serviceName, object data, object auth = null)
    {
        return await WithLeadTypeAsync(_leadTypeAdapter, id, async leadType =>
        {
            var integration = await _integrationAdapter.GetByServiceNameAsync(serviceName);
            if (integration == null) return NotFound();

            // TODO: check if integration is enabled for the org
            // auto enable?
            // ...

            // TODO: encrypt api key
            var authentication = auth != null ? JsonConvert.SerializeObject(auth, SerializationSettings.Default) : null;
            // ...

            var record = await _leadTypeIntegrationAdapter.AddOrUpdateAsync(new LeadTypeIntegration
            {
                LeadTypeId = id,
                IntegrationId = integration.Id,
                Data = JsonConvert.SerializeObject(data, SerializationSettings.Default),
                Authentication = authentication
            });

            return Content(record.Data, "application/json");
        });
    }


    protected async Task<IActionResult> AddIntegrationToEntityAsync(Guid entityId, string serviceName, object data, object auth = null)
    {
        var integration = await _integrationAdapter.GetByServiceNameAsync(serviceName);
        if (integration == null) return NotFound();

        // TODO: encrypt api key
        var authentication = auth != null ? JsonConvert.SerializeObject(auth, SerializationSettings.Default) : null;
        // ...

        var record = await _entityIntegrationAdapter.AddOrUpdateAsync(
            entityId,
            new EntityIntegration
            {
                IntegrationId = integration.Id,
                Data = JsonConvert.SerializeObject(data, SerializationSettings.Default),
                Authentication = authentication
            });

        return Content(record.Data, "application/json");
    }

    protected async Task<IActionResult> AddIntegrationToAppointmentTypeAsync(Guid id, string serviceName, object data, object auth = null)
    {
        // TODO: enforce access rules
        // ...

        var integration = await _integrationAdapter.GetByServiceNameAsync(serviceName);
        if (integration == null) return NotFound();

        var appointmentType = await _appointmentTypeAdapter.GetByIdAsync(id);
        if (appointmentType == null) return NotFound();

        // TODO: check if it is enabled for the org
        // ...

        var user = Context;
        switch (user.Role)
        {
            case EntityRoleId.Manager:
                if (!user.UserId.Value.Equals(appointmentType.EntityId) && !user.OrganizationId.Value.Equals(appointmentType.EntityId))
                {
                    _logger.LogError("Manager {entity} trying to change {appointmentType} {integration}", user.UserId, appointmentType.Id, integration.ServiceName);
                    return Forbid();
                }
                break;

            case EntityRoleId.Admin:
                if (!user.UserId.Value.Equals(appointmentType.EntityId) && !user.AccountId.Value.Equals(appointmentType.EntityId))
                {
                    _logger.LogError("Admin {entity} trying to change {appointmentType} {integration}", user.UserId, appointmentType.Id, integration.ServiceName);
                    return Forbid();
                }
                break;

            case EntityRoleId.Root:
                if (!user.UserId.Value.Equals(appointmentType.EntityId))
                {
                    _logger.LogError("User {entity} trying to change {appointmentType} {integration}", user.UserId, appointmentType.Id, integration.ServiceName);
                    return Forbid();
                }
                break;

            default:
                return Forbid();
        }

        // TODO: encrypt api key
        var authentication = auth != null ? JsonConvert.SerializeObject(auth, SerializationSettings.Default) : null;
        // ...

        var record = await _appointmentTypeIntegrationAdapter.AddOrUpdateAsync(new AppointmentTypeIntegration
        {
            AppointmentTypeId = id,
            IntegrationId = integration.Id,
            Data = JsonConvert.SerializeObject(data, SerializationSettings.Default),
            Authentication = authentication
        });

        return Content(record.Data, "application/json");
    }

    private async Task<IActionResult> WithLeadTypeAsync(
        ILeadTypeAdapter leadTypeAdapter,
        Guid leadTypeId,
        Func<LeadType, Task<IActionResult>> action)
    {
        var leadType = await leadTypeAdapter.GetByIdAsync(leadTypeId);
        if (leadType == null) return NotFound();

        switch (Context.Role)
        {
            case EntityRoleId.Manager:
                if (Context.OrganizationId.Value != leadType.EntityId)
                {
                    return Forbid();
                }
                break;

            case EntityRoleId.Admin:
                if (Context.AccountId.Value != leadType.EntityId)
                {
                    // TODO: check if leadtype is associated with org that belongs to account
                    // ...
                }
                break;

            default:
                return Forbid();
        }

        return await action(leadType);
    }
}

public class IntegrationData<T>
    where T : class
{
    public T Data { get; set; }
    public EntityTrunkLevel Level { get; set; }
    public Guid EntityId { get; set; }

    protected IntegrationData()
    {
    }

    public IntegrationData(IEntityTrunkIntegration dao)
    {
        Level = dao.Level;
        Data = dao.GetData<T>();
    }
}