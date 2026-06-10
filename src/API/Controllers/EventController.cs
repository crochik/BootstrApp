// using System;
// using System.Threading.Tasks;
// using AutoMapper;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using PI.Shared.Controllers;
// using PI.Shared.Data.Adapters;
// using Services;

// namespace Controllers
// {
//     [Route("/api/v1/[controller]")]
//     [Authorize("default")]
//     public class EventController : APIController
//     {
//         private readonly IMapper _mapper;
//         private readonly ILeadEventService _flowService;
//         private readonly ILeadAdapter _leadAdapter;

//         public EventController(
//             IMapper mapper,
//             ILeadEventService flowService,
//             ILeadAdapter leadAdapter)
//         {
//             this._mapper = mapper;
//             this._flowService = flowService;
//             this._leadAdapter = leadAdapter;
//         }

//         // [Authorize("default")]
//         [AllowAnonymous]
//         [HttpPost("/api/v1/Lead({id})/[controller]({eventId})")]
//         public async Task<IActionResult> AddEventAsync(Guid id, Guid eventId)
//         {
//             var lead = await _leadAdapter.GetByIdAsync(id);
//             var body = Request.GetBody();
//             // TODO: enforce access rules  
//             // ... 

//             await _flowService.FireAsync(eventId, lead, body);

//             return Ok();
//         }
//     }
// }
