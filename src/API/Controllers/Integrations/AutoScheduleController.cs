// using System;
// using System.Threading.Tasks;
// using AutoMapper;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Logging;
// using PI.Shared.Controllers;
// using PI.Shared.Data.Adapters;
// using PI.Shared.Data.Models;

// namespace Controllers
// {
//     [Authorize("default")]
//     [Produces("application/json")]
//     [Route("/[controller]/v1/Settings")]
//     public class AutoScheduleController : AbstractIntegrationController
//     {
//         public AutoScheduleController(
//             ILogger<AutoScheduleController> logger,
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
//         [ProducesResponseType(typeof(AutoScheduleIntegrationData), 200)]
//         public Task<IActionResult> GetAutoScheduleUserIntegrationAsync()
//         {
//             var user = Context;
//             return GetEntityeIntegrationDataAsync(user.UserId.Value, "AutoSchedule");
//         }

//         [Authorize("manager")]
//         [HttpPut("/[controller]/v1/User/Settings")]
//         [ProducesResponseType(typeof(AutoScheduleIntegrationData), 200)]
//         public async Task<IActionResult> AddAutoScheduleIntegrationToUserAsync([FromBody] AutoScheduleIntegrationData body)
//         {
//             var user = Context;
//             return await AddAsync(user.UserId.Value, body);
//         }

//         [Authorize("manager")]
//         [HttpGet("/[controller]/v1/Organization/Settings")]
//         [ProducesResponseType(typeof(AutoScheduleIntegrationData), 200)]
//         public Task<IActionResult> GetAutoScheduleOrganizationIntegrationAsync()
//         {
//             var user = Context;
//             return GetEntityeIntegrationDataAsync(user.OrganizationId.Value, "AutoSchedule");
//         }

//         [Authorize("manager")]
//         [HttpPut("/[controller]/v1/Organization/Settings")]
//         [ProducesResponseType(typeof(AutoScheduleIntegrationData), 200)]
//         public async Task<IActionResult> AddAutoScheduleIntegrationToOrganizationAsync([FromBody] AutoScheduleIntegrationData body)
//         {
//             var user = Context;
//             return await AddAsync(user.OrganizationId.Value, body);
//         }

//         private async Task<IActionResult> AddAsync(Guid entityId, AutoScheduleIntegrationData body)
//         {
//             if (body == null)
//             {
//                 return BadRequest();
//             }

//             var result = await AddIntegrationToEntityAsync(entityId, "AutoSchedule", body);
//             return result;
//         }

//         [Authorize("manager")]
//         [HttpGet("/[controller]/v1/LeadType({id})/Settings")]
//         [ProducesResponseType(typeof(AutoScheduleIntegrationData), 200)]
//         public Task<IActionResult> GetAutoScheduleLeadTypeIntegrationAsync(Guid id)
//         {
//             return GetLeadTypeIntegrationAsync(id, "AutoSchedule");
//         }

//         [Authorize("manager")]
//         [HttpPut("/[controller]/v1/LeadType({id})/Settings")]
//         [ProducesResponseType(typeof(AutoScheduleIntegrationData), 200)]
//         public async Task<IActionResult> AddAutoScheduleIntegrationToLeadTypeAsync(Guid id, [FromBody] AutoScheduleIntegrationData body)
//         {

//             if (body == null )
//             {
//                 return BadRequest();
//             }

//             var result = await AddIntegrationToLeadTypeAsync(id, "AutoSchedule", body);
//             return result;
//         }
//     }
// }