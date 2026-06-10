// using System.Collections.Generic;
// using System.Threading.Tasks;
// using AutoMapper;
// using Controllers.Models;
// using Crochik.Messaging;
// using Messages.User;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Logging;
// using PI.Shared.Controllers;
// using PI.Shared.Data.Adapters;
// using PI.Shared.O365;
// using Services;

// namespace Controllers
// {
//     [Route("/api/v1/[controller]")]
//     [Authorize("default")]
//     public class FeatureController : APIController
//     {
//         private readonly ILogger<FeatureController> _logger;
//         private readonly IMapper _mapper;
//         private readonly AppointmentSchedulerService _schedulerService;
//         private readonly IMessageBroker _broker;

//         public FeatureController(
//             ILogger<FeatureController> logger,
//             IMapper mapper,
//             AppointmentSchedulerService schedulerService,
//             IMessageBroker broker
//             )
//         {
//             this._logger = logger;
//             this._mapper = mapper;
//             this._schedulerService = schedulerService;
//             this._broker = broker;
//         }

//         [HttpGet("CalendarSync")]
//         public async Task<IEnumerable<CalendarSyncSettings>> CalendarAsync()
//         {
//             var user = Context;
//             var calendar = await _userAdapter.GetForEntityAsync(user.UserId.Value);

//             var list = new List<CalendarSyncSettings>();
//             foreach (var account in calendar)
//             {
//                 list.Add(_mapper.Map<CalendarSyncSettings>(account));
//             }

//             return list;
//         }

//         [HttpPost("Scheduler")]
//         public async Task SchedulerEnableAsync()
//             => await _schedulerService.EnableAsync(Context.UserId.Value);

//         [HttpPost("CalendarSync")]
//         [ProducesResponseType(typeof(CalendarSyncSettings), 200)]
//         public async Task<IActionResult> CalendarEnableAsync()
//         {
//             await SchedulerEnableAsync();

//             // this will fail if it is not connected to a tenant (e.g. hotmail account)
//             var result = await _calendarService.EnableAsync(Context.UserId.Value);
//             if (!result)
//             {
//                 return StatusCode(422, result.Status);
//             }

//             var o365User = result.Value;
//             var message = new FeatureSetting
//             {
//                 Feature = Feature.O365_Calendar_Sync,
//                 Enable = true,
//                 IdentityId = o365User.IdentityId
//             };

//             await _broker.PublishAsync($"user.{Context.UserId}.feature", message);

//             return Ok(_mapper.Map<CalendarSyncSettings>(o365User));
//         }

//         [HttpDelete("CalendarSync")]
//         public async Task CalendarDisableAsync()
//         {
//             await _calendarService.DisableO365Async(Context.UserId.Value);

//             // TODO: find identity and publish message
//             // ....

//             //var message = new FeatureSetting
//             //{
//             //    Feature = Feature.O365_Calendar_Sync,
//             //    Enable = false,
//             //    IdentityId = o365User.IdentityId.Value
//             //};

//             //await _broker.PublishAsync($"user.{user.Id}.feature", message);
//         }
//     }
// }
