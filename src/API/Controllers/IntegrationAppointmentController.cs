// using System;
// using System.Linq;
// using System.Threading.Tasks;
// using AutoMapper;
// using Controllers.Models;
// using Crochik.Mongo;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using PI.Shared.Controllers;
// using PI.Shared.Data.Adapters;
//
// namespace Controllers
// {
//     /// <summary>
//     /// Called by the integrations to interact with appointments
//     /// </summary>
//     [Produces("application/json")]
//     [Route("/api/v1/[controller]")]
//     public class IntegrationAppointmentController : APIController
//     {
//
//         private readonly IMapper _mapper;
//         private readonly MongoConnection _connection;
//         private readonly IIntegrationAppointmentAdapter _integrationAppointmentAdptr;
//
//         public IntegrationAppointmentController(
//             IMapper mapper,
//             MongoConnection connection,
//             IIntegrationAppointmentAdapter integrationAdapter
//             )
//         {
//             this._mapper = mapper;
//             this._connection = connection;
//             this._integrationAppointmentAdptr = integrationAdapter;
//         }
//
//         [Authorize("default")]
//         [HttpGet("/api/v1/Appointment({id})/Integration")]
//         [ProducesResponseType(typeof(AppointmentIntegration[]), 200)]
//         public async Task<IActionResult> GetIntegrationsAsync(Guid id)
//         {
//             // TODO: enforce user has access to appt
//             // ...
//
//             var integrations = await _integrationAppointmentAdptr.GetAsync(id);
//             var result = integrations.ToList().ConvertAll(i => _mapper.Map<AppointmentIntegration>(i));
//
//             return Ok(result);
//         }
//     }
// }