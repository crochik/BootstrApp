// using System;
// using System.Threading.Tasks;
// using Controllers.Models;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using PI.Shared.Controllers;
// using PI.Shared.Data.Adapters;
//
// namespace Controllers;
//
// [Authorize("default")]
// [Route("/api/v1/[controller]")]
// public class FlowTransitionController : APIController
// {
//     private readonly IOrganizationAdapter _organizationAdapter;
//     private readonly IFlowTransitionAdapter _assignFlowAdapter;
//     private readonly IFlowAdapter _flowAdapter;
//
//     public FlowTransitionController(
//         IOrganizationAdapter organizationAdapter,
//         IFlowTransitionAdapter assignFlowAdapter,
//         IFlowAdapter flowAdapter
//     )
//     {
//         _organizationAdapter = organizationAdapter;
//         _assignFlowAdapter = assignFlowAdapter;
//         _flowAdapter = flowAdapter;
//     }
//
//     [Authorize("admin")]
//     [HttpGet("/api/v1/Flow({flowId})/Entity({entityId})/[controller]({tag})")]
//     [ProducesResponseType(typeof(FlowTransition), 200)]
//     public async Task<IActionResult> GetFlowTransitionAsync(
//         [FromRoute] Guid flowId,
//         [FromRoute] Guid entityId,
//         [FromRoute] string tag
//     )
//     {
//         var result = await _assignFlowAdapter.GetAsync(entityId, flowId, tag);
//         return result == null ? NotFound() : Ok(result);
//     }
//
//     // TODO: make it generic so it also works with org + user
//     [Authorize("admin")]
//     [HttpPost("/api/v1/Flow({flowId})/Entity({entityId})/[controller]({tag})")]
//     [ProducesResponseType(typeof(FlowTransition), 200)]
//     public async Task<IActionResult> AddFlowTransitionAsync(
//         [FromRoute] Guid flowId,
//         [FromRoute] Guid entityId,
//         [FromRoute] string tag,
//         [FromBody] FlowTransition flowTransition
//     )
//     {
//         var org = await _organizationAdapter.GetByIdAsync(entityId);
//         var flow = await _flowAdapter.GetByIdAsync(flowId);
//
//         if (org == null || flow == null)
//         {
//             return NotFound();
//         }
//
//         if (org.AccountId != Context.AccountId || flow.EntityId != Context.AccountId.Value)
//         {
//             return Forbid();
//         }
//
//         if (flowTransition.EntityId != entityId || flowTransition.CurrentFlowId != flowId || flowTransition.Tag != tag)
//         {
//             return BadRequest();
//         }
//
//         var result = await _assignFlowAdapter.AddAsync(Context, flowTransition);
//
//         return Ok(result);
//     }
//
//     [Authorize("admin")]
//     [HttpDelete("/api/v1/Flow({flowId})/Entity({entityId})/[controller]({tag})")]
//     [ProducesResponseType(typeof(FlowTransition), 200)]
//     public async Task<IActionResult> DeleteFlowTransitionAsync(
//         [FromRoute] Guid flowId,
//         [FromRoute] Guid entityId,
//         [FromRoute] string tag
//     )
//     {
//         var org = await _organizationAdapter.GetByIdAsync(entityId);
//         var flow = await _flowAdapter.GetByIdAsync(flowId);
//
//         if (org == null || flow == null)
//         {
//             return NotFound();
//         }
//
//         if (org.AccountId != Context.AccountId || (flow.EntityId != Context.AccountId.Value))
//         {
//             return Forbid();
//         }
//
//         var result = await _assignFlowAdapter.DeleteAsync(Context, entityId, flowId, tag);
//         return result ? Ok(result) : NotFound();
//     }
// }