// using System;
// using System.Linq;
// using System.Threading.Tasks;
// using AutoMapper;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Logging;
// using PI.Shared.Controllers;
// using PI.Shared.Data.Adapters;
// using PI.Shared.Data.Models;
// using PI.Shared.Models;

// namespace Controllers
// {
//     [Authorize("default")]
//     [Produces("application/json")]
//     [Route("/[controller]/v1/Settings")]
//     public class GoToMeetingController : AbstractIntegrationController
//     {
//         public GoToMeetingController(
//             ILogger<GoToMeetingController> logger,
//             IMapper mapper,
//             IEntityIdentityAdapter identityAdapter,
//             IIntegrationAdapter integrationAdapter,
//             ILeadTypeAdapter leadTypeAdapter,
//             IEntityIntegrationAdapter entityIntegrationAdapter,
//             ILeadTypeIntegrationAdapter leadTypeIntegrationAdapter,
//             IAppointmentTypeAdapter appointmentTypeAdapter,
//             IAppointmentTypeIntegrationAdapter appointmentTypeIntegrationAdapter
//         ) : base(logger, mapper, identityAdapter, integrationAdapter, leadTypeAdapter, entityIntegrationAdapter, leadTypeIntegrationAdapter, appointmentTypeAdapter, appointmentTypeIntegrationAdapter)
//         {
//         }

//         [Authorize("manager")]
//         [HttpGet("/[controller]/v1/User/Settings")]
//         [ProducesResponseType(typeof(GoToMeetingIntegrationData), 200)]
//         public Task<IActionResult> GetGoToMeetingUserIntegrationAsync()
//         {
//             var user = Context;
//             return GetEntityeIntegrationDataAsync(user.UserId.Value, "GoToMeeting");
//         }

//         [Authorize("manager")]
//         [HttpPut("/[controller]/v1/User/Settings")]
//         [ProducesResponseType(typeof(GoToMeetingIntegrationData), 200)]
//         public async Task<IActionResult> AddGoToMeetingIntegrationToUserAsync([FromBody] GoToMeetingIntegrationData body)
//         {
//             var user = Context;
//             return await AddAsync(user.UserId.Value, body);
//         }

//         [Authorize("manager")]
//         [HttpGet("/[controller]/v1/Organization/Settings")]
//         [ProducesResponseType(typeof(GoToMeetingIntegrationData), 200)]
//         public Task<IActionResult> GetGoToMeetingOrganizationIntegrationAsync()
//         {
//             var user = Context;
//             return GetEntityeIntegrationDataAsync(user.OrganizationId.Value, "GoToMeeting");
//         }

//         [Authorize("manager")]
//         [HttpPut("/[controller]/v1/Organization/Settings")]
//         [ProducesResponseType(typeof(GoToMeetingIntegrationData), 200)]
//         public async Task<IActionResult> AddGoToMeetingIntegrationToOrganizationAsync([FromBody] GoToMeetingIntegrationData body)
//         {
//             var user = Context;
//             return await AddAsync(user.OrganizationId.Value, body);
//         }

//         private async Task<IActionResult> AddAsync(Guid entityId, GoToMeetingIntegrationData body)
//         {
//             if (body == null)
//             {
//                 return BadRequest();
//             }

//             var result = await AddIntegrationToEntityAsync(entityId, "GoToMeeting", body);
//             return result;
//         }

//         [Authorize("manager")]
//         [HttpGet("/[controller]/v1/AppointmentType({id})/Settings")]
//         [ProducesResponseType(typeof(GoToMeetingIntegrationData), 200)]
//         public Task<IActionResult> GetGoToMeetingAppointmentTypeIntegrationAsync(Guid id)
//         {
//             return GetAppointmentTypeIntegrationDataAsync(id, "GoToMeeting");
//         }

//         [Authorize("manager")]
//         [HttpPut("/[controller]/v1/AppointmentType({id})/Settings")]
//         [ProducesResponseType(typeof(GoToMeetingIntegrationData), 200)]
//         public async Task<IActionResult> AddGoToMeetingIntegrationToAppointmentTypeAsync(Guid id, [FromBody] GoToMeetingIntegrationData body)
//         {
//             // TODO: ?????
//             var user = Context;
//             var identities = await _identityAdapter.GetByEntityAsync(user.UserId.Value, ExternalProvider.GoToMeeting);
//             var array = identities.ToArray();
//             if (array.Length != 1)
//             {
//                 _logger.LogError("Couldn't determine GoToMeeting identity for {usedId}", user.UserId);
//                 return Forbid();
//             }
//             body.EntityId = user.UserId.Value;

//             var result = await AddIntegrationToAppointmentTypeAsync(id, "GoToMeeting", body);
//             return result;
//         }

//         public class GoToMeetingIntegrationData : GoToMeetingIntegration.Data { }
//     }
// }