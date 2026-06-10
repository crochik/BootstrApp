using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace Controllers;

[Route("/api/v1/[controller]")]
[Authorize("default")]
public class AppointmentTypeController : APIController
{
    private static Guid DefaultFlowId = FlowIds.InspireNet;

    private readonly IAppointmentTypeAdapter _appointmentType;
    private readonly ILeadTypeAdapter _leadTypeAdapter;

    public AppointmentTypeController(
        IAppointmentTypeAdapter appointmentType,
        ILeadTypeAdapter leadTypeAdapter
    )
    {
        _appointmentType = appointmentType;
        _leadTypeAdapter = leadTypeAdapter;
    }

    /// <summary>
    /// Return appointment types for Current org (user/manager)
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Authorize("default")]
    public async Task<IEnumerable<AppointmentType>> GetAsync()
    {
        var results = await _appointmentType.GetForOrgAsync(Context);

        return results;
    }

    [HttpGet("/api/v1/[controller]({id})")]
    public async Task<AppointmentType> GetByIdAsync([FromRoute] Guid id)
    {
        // TODO: make sure user has access to it
        // ...

        var result = await _appointmentType.GetByIdAsync(id);
        return result;
    }

    [Authorize("manager")]
    [HttpPost("/api/v1/[controller]")]
    public async Task<AppointmentType> AddAsync(string name)
    {
        var leadType = await _leadTypeAdapter.CreateAsync(new LeadType
        {
            Id = Guid.NewGuid(),
            EntityId = Context.Role == EntityRoleId.Manager ? Context.OrganizationId.Value : Context.UserId.Value,
            Settings = await LoadTemplateAsync<LeadTypeSettings>("AppointmentType_LeadType"),
            Name = name,
            FlowId = DefaultFlowId
        });

        var appointmentType = await _appointmentType.CreateAsync(new AppointmentType
        {
            Id = leadType.Id,
            EntityId = leadType.EntityId,
            Name = leadType.Name,
            LeadTypeId = leadType.Id,
            // Settings = await LoadTemplateAsync<PI.Shared.Data.Models.SchedulingSettings>("AppointmentType")
        });

        // await _appointmentType.SetConfigAsync(appointmentType.Id, AppConfigIds.WebScheduler);

        return appointmentType;
    }

    private async Task<T> LoadTemplateAsync<T>(string filename)
    {
        var path = $"./config/template/{filename}.json";
        var json = await System.IO.File.ReadAllTextAsync(path);
        return JsonConvert.DeserializeObject<T>(json);
    }

    [Authorize("manager")]
    [HttpPatch("/api/v1/[controller]({id})/Settings")]
    public async Task<AppointmentType> UpdateSettingsAsync([FromRoute] Guid id, [FromBody] SchedulingSettings settings)
    {
        if (settings == null) throw new BadRequestException("Missing body");

        var apptType = await _appointmentType.GetByIdAsync(id);
        if (apptType == null) throw NotFoundException.New<AppointmentType>(id);

        // TODO: enforce that the user should have access to this appt type
        // ...

        apptType.Settings = settings;
        var success = await _appointmentType.UpdateAsync(apptType);
        if (!success) throw new Exception("failed to update");
        return apptType;
    }

    [Authorize("manager")]
    [HttpPatch("/api/v1/[controller]({id})")]
    public async Task<AppointmentType> UpdateNameAsync([FromRoute] Guid id, string name)
    {
        if (name == null) throw new BadRequestException("Missing name");

        var apptType = await _appointmentType.GetByIdAsync(id);
        if (apptType == null) throw NotFoundException.New<AppointmentType>(id);

        // TODO: enforce that the user should have access to this appt type
        // ...

        apptType.Name = name;
        var success = await _appointmentType.UpdateAsync(apptType);
        if (!success) throw new Exception("failed to update");

        return apptType;
    }

    [Authorize("manager")]
    [HttpPatch("/api/v1/[controller]({id})/LeadType")]
    public async Task<AppointmentType> UpdateLeadTypeAsync([FromRoute] Guid id, Guid? leadTypeId)
    {
        var apptType = await _appointmentType.GetByIdAsync(id);
        if (apptType == null) throw NotFoundException.New<AppointmentType>(id);

        // TODO: enforce that the user should have access to this appt type
        // ...

        // TODO: should prevent changes if there are any leads and/or delete them first
        // ... 

        if (leadTypeId.HasValue)
        {
            // TODO: implement me
            throw new ForbiddenException(Context);
        }

        if (!apptType.LeadTypeId.HasValue)
        {
            // nothing to do 
            return apptType;
        }

        apptType.LeadTypeId = null;
        var success = await _appointmentType.UpdateAsync(apptType);
        if (!success) throw new Exception("Failed to update");

        return apptType;
    }

    [Authorize("manager")]
    [HttpDelete("/api/v1/[controller]({id})")]
    public Task DeleteAsync([FromRoute] Guid id)
    {
        throw new NotImplementedException();
    }
}