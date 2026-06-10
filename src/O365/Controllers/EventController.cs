using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Authorize]
[Route("/o365/v1/[controller]")]
public class EventController : APIController
{
    private readonly MongoConnection _connection;
    private readonly O365Service _o365;
    private readonly IMapper _mapper;

    public EventController(
        MongoConnection connection,
        O365Service o365,
        IMapper mapper
    )
    {
        _connection = connection;
        _o365 = o365;
        _mapper = mapper;
    }

    // [Authorize("admin")]
    // [HttpGet("/o365/v1/[controller]({id})")]
    // public async Task<IActionResult> GetEventAsync([FromRoute] string id)
    // {
    //     try
    //     {
    //         var evt = await _o365.GetGraphEventAsync(Context, id);
    //         return Ok(evt);
    //     }
    //     catch (Microsoft.Graph.ServiceException ex)
    //     {
    //         throw ex;
    //     }    
    // }

    [Authorize("default")]
    [HttpGet]
    public async Task<IEnumerable<CalendarEvent>> GetEventsAsync([FromQuery] DateTime? start, [FromQuery] DateTime? end)
    {
        var startDate = start ?? DateTime.UtcNow.AddDays(-1).Date;
        var endDate = end ?? startDate.AddDays(15);

        var evts = _o365.GetGraphEvents(Context, startDate, endDate);
        var ret = new List<CalendarEvent>();
        await foreach (var evt in evts)
        {
            ret.Add(_mapper.Map<CalendarEvent>(evt));
        }

        return ret;
    }

    [Authorize("managerplus")]
    [HttpGet("/o365/v1/User({id})/[controller]")]
    public async Task<IEnumerable<CalendarEvent>> GetUserEventsAsync([FromRoute] Guid id, [FromQuery] DateTime? start, [FromQuery] DateTime? end)
    {
        var startDate = start ?? DateTime.UtcNow.AddDays(-1).Date;
        var endDate = end ?? startDate.AddDays(15);

        var query = _connection.UserQuery(Context.AccountId.Value, id);
        if (Context.Role != EntityRoleId.Admin) query.Eq(x => x.OrganizationId, Context.OrganizationId.Value);

        var user = await query.FirstOrDefaultAsync();
        if (user == null) throw NotFoundException.New<User>(id);
        if (Context.Role != EntityRoleId.Admin && user.Id != Context.UserId.Value && user.UserRoleId == nameof(EntityRoleId.Manager)) throw new ForbiddenException(Context);

        var evts = _o365.GetGraphEvents(user.Context, startDate, endDate);
        var ret = new List<CalendarEvent>();
        await foreach (var evt in evts)
        {
            ret.Add(_mapper.Map<CalendarEvent>(evt));
        }

        return ret;
    }
}