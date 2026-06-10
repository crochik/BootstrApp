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
//     public class ZoomController : AbstractIntegrationController
//     {
//         public ZoomController(
//             ILogger<ZoomController> logger,
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
//         [ProducesResponseType(typeof(ZoomIntegrationData), 200)]
//         public Task<IActionResult> GetZoomUserIntegrationAsync()
//         {
//             var user = Context;
//             return GetEntityeIntegrationDataAsync(user.UserId.Value, "Zoom");
//         }

//         [Authorize("manager")]
//         [HttpPut("/[controller]/v1/User/Settings")]
//         [ProducesResponseType(typeof(ZoomIntegrationData), 200)]
//         public async Task<IActionResult> AddZoomIntegrationToUserAsync([FromBody] ZoomIntegrationData body)
//         {
//             var user = Context;
//             return await AddAsync(user.UserId.Value, body);
//         }

//         [Authorize("manager")]
//         [HttpGet("/[controller]/v1/Organization/Settings")]
//         [ProducesResponseType(typeof(ZoomIntegrationData), 200)]
//         public Task<IActionResult> GetZoomOrganizationIntegrationAsync()
//         {
//             var user = Context;
//             return GetEntityeIntegrationDataAsync(user.OrganizationId.Value, "Zoom");
//         }

//         [Authorize("manager")]
//         [HttpPut("/[controller]/v1/Organization/Settings")]
//         [ProducesResponseType(typeof(ZoomIntegrationData), 200)]
//         public async Task<IActionResult> AddZoomIntegrationToOrganizationAsync([FromBody] ZoomIntegrationData body)
//         {
//             var user = Context;
//             return await AddAsync(user.OrganizationId.Value, body);
//         }

//         private async Task<IActionResult> AddAsync(Guid entityId, ZoomIntegrationData body)
//         {
//             if (body == null)
//             {
//                 return BadRequest();
//             }

//             var result = await AddIntegrationToEntityAsync(entityId, "Zoom", body);
//             return result;
//         }

//         [Authorize("manager")]
//         [HttpGet("/[controller]/v1/AppointmentType({id})/Settings")]
//         [ProducesResponseType(typeof(ZoomIntegrationData), 200)]
//         public Task<IActionResult> GetZoomAppointmentTypeIntegrationAsync(Guid id)
//         {
//             return GetAppointmentTypeIntegrationDataAsync(id, "Zoom");
//         }

//         [Authorize("manager")]
//         [HttpPut("/[controller]/v1/AppointmentType({id})/Settings")]
//         [ProducesResponseType(typeof(ZoomIntegrationData), 200)]
//         public async Task<IActionResult> AddZoomIntegrationToAppointmentTypeAsync(Guid id, [FromBody] ZoomIntegrationData body)
//         {
//             // TODO: ?????
//             var user = Context;
//             var identities = await _identityAdapter.GetByEntityAsync(user.UserId.Value, ExternalProvider.Zoom);
//             var array = identities.ToArray();
//             if (array.Length != 1)
//             {
//                 _logger.LogError("Couldn't determine Zoom identity for {usedId}", user.UserId);
//                 return Forbid();
//             }
//             body.EntityId = user.UserId.Value;

//             var result = await AddIntegrationToAppointmentTypeAsync(id, "Zoom", body);
//             return result;
//         }

//         public class ZoomIntegrationData : ZoomIntegration.Data { }
//     }
// }