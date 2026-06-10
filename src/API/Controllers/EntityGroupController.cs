// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using AutoMapper;
// using Controllers.Models;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Logging;
// using PI.Shared.Controllers;
// using PI.Shared.Data.Adapters;

// namespace Controllers
// {
//     [Authorize("default")]
//     [Produces("application/json")]
//     [Route("/api/v1/Entity/Group")]
//     public class EntityGroupController : APIController
//     {
//         private readonly ILogger<EntityGroupController> _logger;
//         private readonly IMapper _mapper;
//         private readonly IEntityGroupAdapter _adapter;

//         public EntityGroupController(
//             ILogger<EntityGroupController> logger,
//             IMapper mapper,
//             IEntityGroupAdapter adapter
//             )
//         {
//             this._logger = logger;
//             this._mapper = mapper;
//             this._adapter = adapter;
//         }

//         [Authorize("default")]
//         [HttpGet]
//         [ProducesResponseType(typeof(IEnumerable<EntityGroup>), 200)]
//         public async Task<IActionResult> GetAsync()
//         {
//             var groups = await _adapter.GetAsync(Context);
//             if (groups == null) return NotFound();

//             return Ok(groups.Select(x => _mapper.Map<EntityGroup>(x)));
//         }

//         [Authorize("managerplus")]
//         [HttpPost]
//         [ProducesResponseType(typeof(EntityGroup), 200)]
//         public async Task<IActionResult> AddAsync([FromBody] EntityGroup model)
//         {
//             var group = await _adapter.CreateAsync(Context, model);
//             if (group == null) return NotFound();

//             return Ok(_mapper.Map<EntityGroup>(group));
//         }

//         [Authorize("managerplus")]
//         [HttpGet("({id})")]
//         [ProducesResponseType(typeof(EntityGroup), 200)]
//         public async Task<IActionResult> GetByIdAsync(Guid id)
//         {
//             var group = await _adapter.GetByIdAsync(Context, id);
//             if (group == null) return NotFound();

//             return Ok(_mapper.Map<EntityGroup>(group));
//         }
//     }
// }