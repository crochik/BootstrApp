using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Authorize("default")]
[Route("/api/v1/[controller]")]
public class StatsController : APIController
{
    private readonly AppointmentSchedulerService _schedulerService;
    private readonly IAppointmentTypeAdapter _appointmentTypeAdapter;
    private readonly ILeadAdapter _leadAdapter;
    private readonly IOrganizationAdapter _organizationAdapter;

    public StatsController(
        AppointmentSchedulerService schedulerService,
        IAppointmentTypeAdapter appointmentTypeAdapter,
        ILeadAdapter leadAdapter,
        IOrganizationAdapter organizationAdapter)
    {
        _schedulerService = schedulerService;
        _appointmentTypeAdapter = appointmentTypeAdapter;
        _leadAdapter = leadAdapter;
        _organizationAdapter = organizationAdapter;
    }
    
    [Authorize("managerplus")]
    [HttpGet("/api/v1/[controller]/AggregateByTool")]
    [ProducesResponseType(typeof(AppointmentAggregation), 200)]
    public async Task<IActionResult> AggregateAppointmentsByToolAsync(DateTime? start, DateTime? end, [FromServices] AppointmentAdapter adapter)
    {
        if (!end.HasValue || end > DateTime.UtcNow) end = DateTime.UtcNow;
        if (!start.HasValue || start < end.Value.AddDays(-30)) start = end.Value.AddDays(-30);

        var aggregation = await adapter.AggregateByToolAsync(Context, start.Value, end.Value);
        return Ok(aggregation);
    }

    [Authorize("admin")]
    [HttpGet("/api/v1/[controller]/Org({id})/Appointments/AggregateByTool")]
    [ProducesResponseType(typeof(AppointmentAggregation), 200)]
    public async Task<IActionResult> AggregateAppointmentsByToolAsync([FromRoute] Guid id, DateTime? start, DateTime? end)
    {
        var org = await _organizationAdapter.GetByIdAsync(id);
        if (org == null) return NotFound();
        if (org.AccountId != Context.AccountId) return Forbid();

        throw new NotImplementedException();
    }
    

    [Authorize("managerplus")]
    [HttpGet("/api/v1/[controller]/Leads")]
    [ProducesResponseType(typeof(LeadAggregation), 200)]
    public Task<IActionResult> GetLeadsPerDayAsync(DateTime? start, DateTime? end)
    {
        return aggregateLeadsPerDayAsync(Context, start, end);
    }

    [Authorize("managerplus")]
    [HttpGet("/api/v1/[controller]/LeadsPerHour")]
    [ProducesResponseType(typeof(LeadAggregation), 200)]
    public Task<IActionResult> GetLeadsPerHourAsync(DateTime? start, DateTime? end)
    {
        return aggregateLeadsPerHourAsync(Context, start, end);
    }

    [Authorize("admin")]
    [HttpGet("/api/v1/[controller]/Org({id})/Leads")]
    [ProducesResponseType(typeof(LeadAggregation), 200)]
    public async Task<IActionResult> AggregateLeadsForOrgAsync([FromRoute] Guid id, DateTime? start, DateTime? end)
    {
        var org = await _organizationAdapter.GetByIdAsync(id);
        if (org == null) return NotFound();

        return await aggregateLeadsPerDayAsync(org.Context, start, end);
    }

    private async Task<IActionResult> aggregateLeadsPerDayAsync(IEntityContext context, DateTime? start, DateTime? end)
    {
        if (!Context.CanAccess(context)) return Forbid();

        var startDate = start ?? DateTime.UtcNow.Subtract(TimeSpan.FromDays(30));
        var endDate = end ?? DateTime.UtcNow;
        if (endDate.Subtract(startDate).TotalDays >= 31) return BadRequest();

        var result = await _leadAdapter.AggregateAsync(context, startDate, endDate);

        return Ok(result);
    }

    [Authorize("admin")]
    [HttpGet("/api/v1/[controller]/Org({id})/LeadsPerHour")]
    [ProducesResponseType(typeof(LeadAggregation), 200)]
    public async Task<IActionResult> AggregateLeadsForOrgByHourAsync([FromRoute] Guid id, DateTime? start, DateTime? end)
    {
        var org = await _organizationAdapter.GetByIdAsync(id);
        if (org == null) return NotFound();

        return await aggregateLeadsPerHourAsync(org.Context, start, end);
    }

    private async Task<IActionResult> aggregateLeadsPerHourAsync(IEntityContext context, DateTime? start, DateTime? end)
    {
        if (!Context.CanAccess(context)) return Forbid();

        var startDate = start ?? DateTime.UtcNow.Subtract(TimeSpan.FromDays(7));
        var endDate = end ?? DateTime.UtcNow;
        if (endDate.Subtract(startDate).TotalDays >= 8) return BadRequest();

        var result = await _leadAdapter.AggregatePerHourAsync(context, startDate, endDate);

        return Ok(result);
    }

    [Authorize("manager")]
    [HttpGet("/api/v1/[controller]/Availability")]
    [ProducesResponseType(typeof(AvailabilityStats), 200)]
    public Task<IActionResult> GetOrgAvailabilityAsync(DateTime? start, DateTime? end)
    {
        return getOrgAvailabilityForTypeAsync(Context, null, start, end);
    }

    [Authorize("admin")]
    [HttpGet("/api/v1/[controller]/Org({id})/Availability")]
    [ProducesResponseType(typeof(AvailabilityStats), 200)]
    public async Task<IActionResult> GetAvailabilityForOrgAsync(Guid id, DateTime? start, DateTime? end)
    {
        var org = await _organizationAdapter.GetByIdAsync(id);
        if (org == null) return NotFound();

        return await getOrgAvailabilityForTypeAsync(org.Context, null, start, end);
    }

    [Authorize("managerplus")]
    [HttpGet("/api/v1/[controller]/AppointmentType(id)/Availability")]
    [ProducesResponseType(typeof(AvailabilityStats), 200)]
    public Task<IActionResult> GetOrgAvailabilityForTypeAsync(Guid id, DateTime? start, DateTime? end)
    {
        return getOrgAvailabilityForTypeAsync(Context, id, start, end);
    }

    private async Task<IActionResult> getOrgAvailabilityForTypeAsync(IEntityContext context, Guid? id, DateTime? start, DateTime? end)
    {
        if (!Context.CanAccess(context)) return Forbid();

        AppointmentType appointmentType;
        if (id.HasValue)
        {
            appointmentType = await _appointmentTypeAdapter.GetByIdAsync(id.Value);
            if (appointmentType == null) return NotFound();
            if (!appointmentType.EntityId.Equals(context.OrganizationId.Value)) return Forbid();
        }
        else
        {
            appointmentType = await _appointmentTypeAdapter.GetDefaultForOrgAsync(context, null);
            if (appointmentType == null) return BadRequest();
        }

        // TODO: limit access 
        // switch (context.Role)
        // {
        //     case EntityRoleId.Manager:
        //     case EntityRoleId.Admin:
        //     case EntityRoleId.Organization:
        //         if (appointmentType.EntityId != context.AccountId &&)
        //             break;

        //     default:
        //         return Forbid();
        // }

        // fix range
        var startDate = start ?? DateTime.UtcNow;
        var endDate = end ?? startDate.AddDays(15);

        var stats = new AvailabilityStats
        {
            Start = startDate,
            End = endDate,
            AppointmentTypeId = appointmentType.Id,
            DurationInMinutes = appointmentType.Settings.Duration,
        };

        var result = await _schedulerService.AggregateAsync(appointmentType, startDate, endDate);
        if (result == null || result.Entities.Count < 1)
        {
            return Ok(stats);
        }

        foreach (var entity in result.Entities)
        {
            var rows = new Dictionary<DateTime, AvailabilityStats.Row>();

            foreach (var slot in entity.Value.Slots)
            {
                // convert time to local time before grouping by day
                var date = TimeZoneInfo.ConvertTime(slot.Start, TimeZoneInfo.Utc, entity.Value.TimeZoneInfo).Date;
                date = TimeZoneInfo.ConvertTime(date, entity.Value.TimeZoneInfo, TimeZoneInfo.Utc);

                var duration = slot.End - slot.Start;
                int nSlots = (int)Math.Floor(duration.TotalMinutes / stats.DurationInMinutes);
                if (nSlots < 1) continue;
                if (rows.TryGetValue(date, out var row))
                {
                    row.OpenSlotsCount += nSlots;
                }
                else
                {
                    var newRow = new AvailabilityStats.Row
                    {
                        UserId = entity.Key,
                        Date = date,
                        OpenSlotsCount = nSlots
                    };
                    rows.Add(date, newRow);
                }
            }

            foreach (var evt in entity.Value.Events)
            {
                if (evt.Source.Equals("o365")) continue;

                // convert time to local time before grouping by day
                var date = TimeZoneInfo.ConvertTime(evt.Start, TimeZoneInfo.Utc, entity.Value.TimeZoneInfo).Date;
                date = TimeZoneInfo.ConvertTime(date, entity.Value.TimeZoneInfo, TimeZoneInfo.Utc);

                if (rows.TryGetValue(date, out var row))
                {
                    row.AppointmentsCount++;
                }
                else
                {
                    var newRow = new AvailabilityStats.Row
                    {
                        UserId = entity.Key,
                        Date = date,
                        AppointmentsCount = 1
                    };
                    rows.Add(date, newRow);
                }
            }

            stats.Data.AddRange(rows.Values);
        }

        return Ok(stats);
    }
}

public class AvailabilityStats
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public Guid AppointmentTypeId { get; set; }
    public int DurationInMinutes { get; set; }
    public List<Row> Data { get; set; } = new List<Row>();

    public class Row
    {
        public Guid UserId { get; set; }
        public DateTime Date { get; set; }
        public int AppointmentsCount { get; set; }
        public int OpenSlotsCount { get; set; }
    }
}