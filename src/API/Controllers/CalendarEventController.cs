// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using AutoMapper;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using PI.Shared.Controllers;
// using PI.Shared.Services;

// namespace Controllers
// {
//     [Route("/api/v1/[controller]")]
//     [Authorize("default")]
//     public class CalendarEventController : APIController
//     {
//         private readonly IMapper _mapper;
//         private readonly AppointmentSchedulerService _schedulerService;

//         public CalendarEventController(
//             IMapper mapper,
//             AppointmentSchedulerService schedulerService)
//         {
//             this._mapper = mapper;
//             this._schedulerService = schedulerService;
//         }

//         [Authorize("default")]
//         [HttpGet]
//         public async Task<IEnumerable<PI.Shared.Models.CalendarEvent>> MyEventsAsync(
//             DateTime? start, DateTime? end)
//         {
//             var startDate = start ?? DateTime.UtcNow.AddDays(-1);
//             var endDate = end ?? startDate.AddDays(30);

//             var events = await _schedulerService.GetEventsAsync(Context, startDate, endDate);

//             // TODO: should also load from other sources? 
//             // other calendars / appointments?
//             // ...

//             return events.Select(x => _mapper.Map<PI.Shared.Models.CalendarEvent>(x));
//         }

//         // [Authorize("root")]
//         // [HttpGet("/api/v1/User({userId})/Calendar/Event")]
//         // public async Task<IEnumerable<Event>> UserEventsAsync([FromRoute] string userId, DateTime? start, DateTime? end)
//         // {
//         //     var startDate = start ?? DateTime.UtcNow.AddDays(-1);
//         //     var endDate = end ?? startDate.AddDays(30);
//         //     var events = await _eventAdapter.GetAsync(userId, startDate, endDate);
//         //     return Map(events);
//         // }

//         // [Authorize("default")]
//         // [HttpGet("/api/v1/User/Calendar/Event")]
//         // [ProducesResponseType(typeof(Event[]), 200)]
//         // public async Task<IActionResult> MyEventsAsync(DateTime? start, DateTime? end)
//         // {
//         //     var user = this.AuthenticatedUser();
//         //     var calendarUserId = await _user.GetCalendarUserAsync(user);
//         //     if (calendarUserId == null)
//         //     {
//         //         // ... 
//         //         return NotFound();
//         //     }

//         //     var events = await _eventRepo.GetAsync(calendarUserId, start, end);

//         //     var results = new List<Event>();
//         //     foreach (var evt in events)
//         //     {
//         //         results.Add(_mapper.Map<Event>(evt));
//         //     }

//         //     return Ok(results);
//         // }
//     }
// }
