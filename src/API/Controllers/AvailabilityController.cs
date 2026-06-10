using AutoMapper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Mongo.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Route("/api/v1/[controller]")]
[Authorize("default")]
public class AvailabilityController : APIController
{
    private readonly ILogger<AvailabilityController> _logger;
    private readonly MongoConnection _connection;
    private readonly IMapper _mapper;
    private readonly AppointmentSchedulerService _scheduler;
    private readonly AvailabilityAdapter _availability;
    private readonly IUserAdapter _userAdapter;

    public AvailabilityController(
        ILogger<AvailabilityController> logger,
        MongoConnection connection,
        IMapper mapper,
        AppointmentSchedulerService scheduler,
        AvailabilityAdapter availability,
        IUserAdapter userAdapter
    )
    {
        _logger = logger;
        _connection = connection;
        _mapper = mapper;
        _scheduler = scheduler;
        _availability = availability;
        _userAdapter = userAdapter;
    }

    [Authorize("default")]
    [HttpGet]
    public async Task<IEnumerable<Availability>> AvailabilityAsync()
    {
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, Context.UserId.Value)
            .FirstOrDefaultAsync();

        if (user == null) throw NotFoundException.New<User>(Context.UserId.Value);

        return user.Availability ?? Enumerable.Empty<Availability>();
    }

    [Authorize("default")]
    [HttpDelete]
    public Task DeleteAvailabilityAsync()
    {
        throw new NotImplementedException();
    }

    [Authorize("default")]
    [HttpDelete("/api/v1/[controller]({id})")]
    public async Task<IActionResult> DeleteAvailabilityAsync([FromRoute] Guid id)
    {
        var user = Context;
        var result = await _availability.DeleteAsync(user.UserId.Value, id);
        return result ? Ok() : NotFound();
    }

    [Authorize("default")]
    [HttpPut]
    public async Task<IEnumerable<Availability>> PutAvailabilityAsync([FromBody] Availability[] availability)
    {
        foreach (var a in availability)
        {
            if (a.Id == Guid.Empty) a.Id = Guid.NewGuid();
        }

        var modified = availability.ToLookup(x => x.Id);

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, Context.UserId.Value)
            .FirstOrDefaultAsync();

        if (user == null) throw NotFoundException.New<User>(Context.UserId.Value);

        var result = user.Availability?.Where(x => !modified.Contains(x.Id)) ?? Array.Empty<Availability>();
        var array = result.Concat(availability).ToArray();

        user = await _connection.UpdatePropertyAsync<User, Availability[]>(
            user.Id,
            x => x.Availability,
            array
        );

        return user.Availability.Where(x => modified.Contains(x.Id));
    }

    [Authorize("default")]
    [HttpGet("/api/v1/AppointmentType({id})/[controller]")]
    public Task<AppointmentTypeAvailability> AvailabilityAsync([FromRoute] Guid id)
    {
        var user = Context;
        return _scheduler.GetAppointmentTypeAvailabilityAsync(user.UserId.Value, id);
    }

    [Authorize("managerplus")]
    [HttpPost("/api/v1/User/[controller]/DataView")]
    public async Task<DataViewResponse> GetUserAvailabilityAsync([FromBody] DataViewRequest request)
    {
        return await _scheduler.GetUserAvailabilityAsync(Context, request);
    }

    [Authorize("admin")]
    [HttpPost("/api/v1/Organization/[controller]/DataView")]
    public async Task<DataViewResponse> GetOrgAvailabilityAsync([FromBody] DataViewRequest request)
    {
        return await _scheduler.GetOrgAvailabilityAsync(Context, request);
    }

    [Authorize("default")]
    [HttpGet("/api/v1/AppointmentType({id})/Slot")]
    public async Task<IEnumerable<TimeSlot>> AllSlotsAsync([FromRoute] Guid id, DateTime? start, DateTime? end)
    {
        var startDate = start ?? DateTime.UtcNow.AddDays(-1).Date;
        var endDate = end ?? startDate.AddDays(30);

        var result = await _scheduler.GetAllSlotsAsync(Context, id, startDate, endDate);
        return result.Result.Slots;
    }

    [Authorize("default")]
    [HttpGet("/api/v1/AppointmentType({id})/Open")]
    public async Task<IEnumerable<TimeSlot>> OpenSlotsAsync([FromRoute] Guid id, DateTime? start, DateTime? end)
    {
        var startDate = start ?? DateTime.UtcNow.AddDays(-1).Date;
        var endDate = end ?? startDate.AddDays(30);

        var result = await _scheduler.GetOpenSlotsAsync(Context, id, startDate, endDate);
        return result.Result.Slots;
    }

    // [Authorize("default")]
    // [HttpGet("/api/v1/AppointmentType({id})/Open+Event")]
    // public async Task<EntityOpenSlots> CalendarAsync([FromRoute] Guid id, DateTime? start, DateTime? end)
    // {
    //     var startDate = start ?? DateTime.UtcNow.AddDays(-1).Date;
    //     var endDate = end ?? startDate.AddDays(30);

    //     var result = await _scheduler.GetOpenSlotsAsync(Context, id, startDate, endDate);
    //     return result.Result;
    // }

    [Authorize("manager")]
    [HttpGet("/api/v1/Organization/[controller]")]
    public async Task<IEnumerable<TimeSlotWithCount>> OrganizationAvailabilityAsync(DateTime? start, DateTime? end)
    {
        var startDate = start ?? DateTime.UtcNow.AddDays(-1).Date;
        var endDate = end ?? startDate.AddDays(30);

        var result = await _scheduler.GetOrganizationAvailabilityAsync(Context, startDate, endDate);

        return result;
    }

    [Authorize("admin")]
    [HttpGet("/api/v1/Organization({id})/[controller]")]
    public async Task<IEnumerable<TimeSlotWithCount>> OrganizationAvailabilityAsync([FromRoute] Guid id, DateTime? start, DateTime? end)
    {
        var org = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (org == null) throw NotFoundException.New<Organization>(id);

        var startDate = start ?? DateTime.UtcNow.AddDays(-1).Date;
        var endDate = end ?? startDate.AddDays(30);

        var result = await _scheduler.GetOrganizationAvailabilityAsync(org.Context, startDate, endDate);

        return result;
    }

    [Authorize("default")]
    [HttpGet("/api/v1/User({userId})/AppointmentType({appointmentTypeId})/[controller]")]
    [ProducesResponseType(typeof(EntityOpenSlots), 200)]
    public async Task<EntityOpenSlots> UserAvailabilityAsync([FromRoute] Guid userId, [FromRoute] Guid appointmentTypeId, DateTime? start, DateTime? end)
    {
        var user = await _userAdapter.GetByIdAsync(userId);
        if (!Context.CanAccess(user)) throw new ForbiddenException(Context);

        var startDate = start ?? DateTime.UtcNow.AddDays(-1).Date;
        var endDate = end ?? startDate.AddDays(30);
        var result = await _scheduler.GetOpenSlotsAsync(user.Context, appointmentTypeId, startDate, endDate);

        if (!user.Id.Equals(Context.EntityId.Value))
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