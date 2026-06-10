// using System;
// using System.Threading.Tasks;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using PI.Shared.Constants;
// using PI.Shared.Controllers;
// using PI.Shared.Services;
// using Services;
//
// namespace Controllers
// {
//     [Route("/salesforce/v1/[controller]")]
//     public class AppointmentController : APIController
//     {
//         [Authorize("admin")]
//         [HttpPost("/salesforce/v1/Token")]
//         public async Task<IActionResult> GetTokenAsync([FromServices] SalesforceService service)
//         {
//             var (token, error) = await service.GetTokenAsync(AccountIds.FCI);
//             if (error != null) throw new Exception($"Failed to get Token: {error}");
//             return Ok(token);
//         }
//
//         [Authorize("admin")]
//         [HttpPost("/salesforce/v1/[controller]({id})")]
//         public async Task<IActionResult> AddAppointmentToSalesforceAsync(
//             [FromRoute] Guid id,
//             [FromServices] SalesforceLeadService service
//             )
//         {
//             var appointment = await service.ExportAppointmentAsync(Context, id);
//             return Ok(appointment);
//         }
//
//         [Authorize("admin")]
//         [HttpDelete("/salesforce/v1/[controller]({id})")]
//         public async Task<IActionResult> CancelAppointmentAsync(
//             [FromRoute] string id,
//             [FromServices] SalesforceService service,
//             [FromServices] PI.Shared.Salesforce.ISalesforceClient salesforceClient
//             )
//         {
//             var (token, error) = await service.GetTokenAsync(AccountIds.FCI);
//             await salesforceClient.UpdateAsync(token, "ServiceAppointment", id, new {
//                 Status = "Canceled",
//             });
//
//             return Ok();
//         }
//
//     }
// }