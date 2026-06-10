// using System;
// using System.Threading.Tasks;
// using Crochik.Mongo;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Logging;
// using PI.Shared.Controllers;
// using PI.Shared.Exceptions;
// using PI.Shared.Models;
// using Services;
//
// namespace Controllers;
//
// [Route("/twilio/v1/[controller]")]
// [Authorize("admin")]
// public class LeadController : APIController
// {
//     private readonly ILogger<LeadController> _logger;
//     private readonly MongoConnection _connection;
//     private readonly SMSService _smsService;
//
//     public LeadController(ILogger<LeadController> logger, MongoConnection connection, SMSService smsService)
//     {
//         _logger = logger;
//         _connection = connection;
//         _smsService = smsService;
//     }
//
//     [HttpPost("/twilio/v1/[controller]({id})")]
//     public async Task<CommunicationNote> SendSMSToLeadAsync([FromRoute] Guid id, [FromQuery] string message)
//     {
//         var lead = await _connection.Filter<Lead>()
//             .Eq(x => x.AccountId, Context.AccountId)
//             .Eq(x => x.Id, id)
//             .FirstOrDefaultAsync();
//
//         if (lead == null) throw NotFoundException.New<Lead>(id);
//
//         return await _smsService.SendAsync(Context, lead, message);
//     }
// }