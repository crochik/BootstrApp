using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;
using Services;

namespace Controllers;

[Authorize("lumin-lead")]
[Route("/lumin/v1/[controller]")]
public class LeadController : AbstractLeadConversionIntegrationController
{
    public LeadController(
        ILogger<LeadController> logger,
        IMapper mapper,
        MongoConnection connection,
        AppointmentSchedulerService schedulerService,
        ObjectTypeService objectTypeService,
        ILeadConversionIntegrationService integrationService
    ) : base(logger, mapper, connection, schedulerService, objectTypeService, integrationService)
    {
    }

    [HttpGet]
    public Task<LeadResp> GetLeadResp() => GetLeadRespAsync();

    [HttpGet("Slots")]
    public Task<IEnumerable<TimeSlot>> GetSlots([FromQuery] DateTime? start, [FromQuery] DateTime? end) => GetSlotsAsync(start, end);

    [HttpPost("Note")]
    [RequestSizeLimit(10_000)]
    public async Task<IActionResult> AddNote() // [FromBody] AddNoteReq request
    {
        await Task.CompletedTask;
        
        _logger.LogInformation("Received Note Request: {Body}", Request.GetBody());
        // return AddNoteAsync(request);
        return Ok();
    }

    [Obsolete("replaced with POST /lumin/v1/Appointment")]
    [HttpPost("Appointment")]
    public async Task<AppointmentResp> AppointmentAsync([FromBody] AppointmentReq appt)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId
        });

        _logger.LogInformation("Schedule Appointment (obsolete)");

        return await ScheduleAppointmentAsync(appt);
    }

    [HttpPost("Action")]
    public async Task<IActionResult> AddEventasync([FromQuery] string action)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId,
            Action = action
        });

        _logger.LogInformation("Received Event");

        var actionValue = action switch
        {
            "reached-out" => Action.ReachedOut,
            "newlead-firstresponse" => Action.FirstResponse,
            "newlead-ineligible" => Action.Ineligible,
            "opt-out" => Action.OptOut,
            "plan-change" => Action.PlanChange,
            "appt-scheduled" => Action.ApptScheduled,
            "appt-rescheduled" => Action.ApptRescheduled,
            "appt-canceled" => Action.ApptCancelled,
            "request-csr" => Action.RequestCSR,
            _ => default(Action?),
        };

        if (!actionValue.HasValue) throw new BadRequestException("Unexpected action");

        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, LeadId)
            .FirstOrDefaultAsync();

        if (lead == null) throw new Exception("Invalid Lead");

        var integration = lead.Integrations?.FirstOrDefault(x => x.IntegrationId == _integrationService.IntegrationId);
        if (integration == null) throw new Exception("Missing integration info");

        switch (actionValue)
        {
            case Action.ReachedOut:
                await UpdateStatusAsync("Reached Out", nameof(IExternalLeadIntegration.ReachedOut));
                await _integrationService.AddNoteAsync(Context, lead, "Lumin Reached Out");
                break;

            case Action.FirstResponse:
                await UpdateStatusAsync("Engaged", nameof(IExternalLeadIntegration.FirstResponse));
                await _integrationService.AddNoteAsync(Context, lead, "Lumin Engaged");
                break;

            case Action.Ineligible:
                await UpdateStatusAsync("Ineligible", nameof(LuminLeadIntegration.Ineligible));
                await _integrationService.AddNoteAsync(Context, lead, "Lumin Flagged as Ineligible");
                break;

            case Action.OptOut:
                await UpdateStatusAsync("Opted Out", nameof(IExternalLeadIntegration.OptOut));
                await _integrationService.AddNoteAsync(Context, lead, "Customer Opted Out via Lumin");
                break;

            case Action.RequestCSR:
                await UpdateStatusAsync("Requested CSR", nameof(LuminLeadIntegration.RequestCSR));
                await _integrationService.AddNoteAsync(Context, lead, "Requested Customer Rep via Lumin");
                break;

            case Action.ApptCancelled:
                if (await _schedulerService.CancelFutureAppointmentsAsync(Context, lead, _integrationService.IntegrationId))
                {
                    await SetStatusAsync("Appointment Cancelled");
                }

                break;

            case Action.ApptScheduled:
            case Action.ApptRescheduled:
                // nothing to do 
                break;
        }
        
        // TODO: fire events on the lead?
        // ...

        return Ok();
    }

    private async Task SetStatusAsync(string status, bool optOut = false)
    {
        var query = _connection.Filter<Lead>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.Id, LeadId)
                .ElemMatchBuilder(
                    x => x.Integrations,
                    q => q.Eq(x => x.IntegrationId, _integrationService.IntegrationId)
                )
                .Update
                .Set($"{nameof(Lead.Integrations)}.$.{nameof(LeadIntegration.Status)}", status)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, Context.Actor)
            ;

        // if (optOut)
        // {
        //     // change comm preferences
        //     query.Set(x => x.CommunicationPreferences[CommunicationChannel.SMS], CommunicationPreference.OptedOut);
        // }

        var result = await query.UpdateAndGetOneAsync();

        if (result == null) return;

        _logger.LogInformation("Changed status to {Status} / {OptOut}", status, optOut);

        var modifiedFields = new Dictionary<string, object>
        {
            { $"{nameof(Lead.Integrations)}|{_integrationService.IntegrationId}|{nameof(LeadIntegration.Status)}", status } 
        };

        var name = IntegrationIds.GetName(_integrationService.IntegrationId);
        await _objectTypeService.FireObjectUpdatedAsync(Context, result, modifiedFields, e =>
        {
            e.Description = $"{name} changed status to {status}";
            e.SetRefValue(nameof(Integration), _integrationService.IntegrationId);
            e.SetMetaValue(nameof(Integration), name);
            if (optOut) e.SetMetaValue("OptOut", optOut);
        });
    }

    public enum Action
    {
        // Lumin texted outbound for the first time
        ReachedOut,

        // Homeowner texted back for the first time
        FirstResponse,

        // (means that Lumin did not start a conversation because of business logic, e.g., 
        // the lead has no textable phone number, or the lead owner’s calendar is unknown)
        Ineligible,

        // lead opted out
        OptOut,

        // homeowner has gone in a different direction
        PlanChange,

        // scheduled
        ApptScheduled,

        // appt rescheduled
        ApptRescheduled,

        // appt cancelled
        ApptCancelled,

        // (means that the homeowner asked to talk to a person)
        RequestCSR,
    }
}