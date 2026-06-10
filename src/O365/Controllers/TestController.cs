// using System;
// using System.Threading;
// using System.Threading.Tasks;
// using Crochik.NET.APM;
// using Microsoft.AspNetCore.Mvc;
// using PI.Shared.Data;
// using PI.Shared.O365;
// using Services;

// namespace Controllers
// {
//     public class TestController : ControllerBase
//     {
//         public TestController()
//         {
//         }

//         // [HttpPost("Event")]
//         // public async Task<IActionResult> EnableAsync(Guid entityId, string subject, DateTime start, DateTime end)
//         // {
//         //     string timeZoneId = "America/New_York";
//         //     var evt = await _calendar.AddEventAsync(entityId, subject, timeZoneId, start, end);
//         //     return Ok(evt);
//         // }

//         //         [HttpPost("Enable")]
//         //         public async Task<IActionResult> EnableAsync(Guid entityId)
//         //         {
//         //             var o365User = await _calendar.EnableAsync(entityId);
//         //             return Ok();
//         //         }

//         //         [HttpPost("Subscribe")]
//         //         public async Task<IActionResult> SubscribeAsync(string userId)
//         //         {
//         //             // await _orchestrator.RefreshEventsAsync(identityId);
//         //             // await _orchestrator.CreateOrRenewSubscriptionAsync(identityId);

//         //             var client = await ((CalendarService)_calendar).GetClientAsync(userId);
//         //             client.BaseUrl = "https://graph.microsoft.com/beta";

//         //             var subscription = new Microsoft.Graph.Subscription
//         //             {
//         //                 ChangeType = "created,updated,deleted",
//         //                 Resource = $"users/{userId}/events",
//         //                 NotificationUrl = $"https://o365.fci.cloud/Notification/MSA/{userId}",
//         //                 ClientState = "SomeSecret",
//         //                 ExpirationDateTime = DateTime.UtcNow.AddMinutes(4200) // max is 3 days (4230 minutes)
//         //             };

//         //             try
//         //             {
//         //                 var request = client.Subscriptions.Request();
//         //                 System.Console.WriteLine(request.RequestUrl);
//         //                 var result = await request.AddAsync(subscription);

//         //             }
//         //             catch (Exception ex)
//         //             {
//         //                 System.Console.WriteLine(ex);
//         //             }

//         //             return Ok();
//         //         }
//     }
// }