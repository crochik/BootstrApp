using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Route("/api/v1/[controller]")]
[Authorize("default")]
public class CalendarController : APIController
{
    private readonly MongoConnection _connection;
    private readonly AppointmentSchedulerService _schedulerService;

    public CalendarController(
        MongoConnection connection,
        AppointmentSchedulerService schedulerService
    )
    {
        _connection = connection;
        _schedulerService = schedulerService;
    }

    [Authorize("default")]
    [HttpGet]
    public async Task<EntityOpenSlots> GetMyCalendarAsync(DateTime? start, DateTime? end)
    {
        var startDate = start ?? DateTime.UtcNow.AddDays(-1).Date;
        var endDate = end ?? startDate.AddDays(30);

        var result = await _schedulerService.GetOpenSlotsAsync(Context, startDate, endDate);
        return result?.Result ?? new EntityOpenSlots();
    }

    [Authorize("default")]
    [HttpPost("/api/v1/[controller]/DataView")]
    [HttpPost("/api/v1/Lead({leadId})/[controller]/DataView")]
    [Produces("text/csv", "application/json")]
    public async Task<IDataViewResponse> GetCalendarDataViewAsync(
        [FromServices] CalendarViewBuilder builder,
        [FromBody] DataViewRequest request, [FromRoute] Guid? leadId = null)
    {
        var result = await builder.BuildAsync(Context, request, false, leadId);
        return result;
    }

    [Authorize("default")]
    [HttpPost("/api/v1/[controller]/Agenda/DataView")]
    [HttpPost("/api/v1/Lead({leadId})/[controller]/Agenda/DataView")]
    [Produces("text/csv", "application/json")]
    public async Task<IDataViewResponse> GetAgendaDataViewAsync(
        [FromServices] CalendarViewBuilder builder,
        [FromBody] DataViewRequest request, [FromRoute] Guid? leadId = null)
    {
        var result = await builder.BuildAsync(Context, request, true, leadId);
        return result;
    }

    [Authorize("default")]
    [HttpGet("/api/v1/User({id})/[controller]")]
    public async Task<EntityOpenSlots> GetUserCalendarAsync([FromRoute] Guid id, DateTime? start, DateTime? end)
    {
        var query = _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id);

        switch (Context.Role)
        {
            case EntityRoleId.Admin:
                break;

            case EntityRoleId.Manager:
                query.Eq(x => x.OrganizationId, Context.OrganizationId.Value);
                break;

            case EntityRoleId.User:
                if (Context.UserId.Value != id) throw new ForbiddenException(Context);
                break;

            default:
                throw new ForbiddenException(Context);
        }

        var user = await query.FirstOrDefaultAsync();
        if (user == null) throw NotFoundException.New<User>(id);

        var startDate = start ?? DateTime.UtcNow.AddDays(-1).Date;
        var endDate = end ?? startDate.AddDays(30);

        var result = await _schedulerService.GetOpenSlotsAsync(Context, startDate, endDate);

        if (id != Context.UserId.Value)
        {
            // scrub events 
            var index = 0;
            foreach (var evt in result.Result.Events)
            {
                evt.Id = (++index).ToString();
                evt.Subject = evt.ShowAs.ToString();
                evt.WebLink = null;
            }
        }

        return result.Result;
    }
}