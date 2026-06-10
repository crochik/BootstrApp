using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Models;
using Services;

namespace Controllers;

[Route("/sendgrid/v1/[controller]")]
[Authorize("admin")]
public class EmailController : APIController
{
    [HttpPost]
    public async Task<IActionResult> SendEmailAsync(
        [FromBody] EmailMessage emailsend,
        [FromServices] SendGridEmailService emailService
        )
    {
        SendGridEmailService.DuplicatePropertiesUsingUnderscore(emailsend.TemplateData);

        var result = await emailService.SendEmailAsync(Context, emailsend);
        return Ok(result);
    }

//     [AllowAnonymous]
//     [HttpPost("test/template")]
//     public async Task<IActionResult> SendTestTemplateEmailAsync(
//         [FromServices] SendGridEmailService emailService,
//         [FromServices] IEntityIntegrationAdapter adapter
//     )
//     {
//         var list = await adapter.GetTrunkByIdAsync(AccountIds.FCI, IntegrationIds.SendGrid);
//         var ordered = list.OrderBy(x => x.Level).ToArray();
//         var data = ordered.FirstOrDefault()?.GetData<SendGridIntegration.Data>();
//         var auth = ordered.FirstOrDefault()?.GetAuthentication<SendGridIntegration.Authentication>();
//         if (string.IsNullOrEmpty(data?.FromEmail) || string.IsNullOrEmpty(data?.FromName) || string.IsNullOrEmpty(auth?.APIKey))
//         {
//             return Forbid("Integration configuration not found");
//         }
//
//         var msg = new SendGridMessage()
//         {
//             From = new SendGrid.Helpers.Mail.EmailAddress
//             {
//                 Name = data.FromName,
//                 Email = data.FromEmail,
//             },
//             Subject = $"test app: {DateTime.Now.TimeOfDay}",
//             // PlainTextContent = email.PlainBody,
//             // HtmlContent = email.HtmlBody,
//         };
//
//         msg.AddTo(new SendGrid.Helpers.Mail.EmailAddress
//         {
//             Name = "Felipe",
//             Email = "felipe@schedonl.onmicrosoft.com",
//         });
//
//         msg.AddCcs(new List<EmailAddress>
//         {
//             new SendGrid.Helpers.Mail.EmailAddress
//             {
//                 Name = "Felipe",
//                 Email = "felipe@crochik.com",
//             },
//             new SendGrid.Helpers.Mail.EmailAddress
//             {
//                 Name = "Ryan",
//                 Email = "raschauer@floorcoveringsinternational.com",
//             }
//         });
//
//         
//         msg.TemplateId = "d-f81d1ee497544c80aaae73f764e5e7c6";
//         msg.SetTemplateData(new
//         {
//             Nada = "",
//         });
//         
//         var ical = @"BEGIN:VCALENDAR
// PRODID:-//github.com/rianjs/ical.net//NONSGML ical.net 4.0//EN
// VERSION:2.0
// METHOD:PUBLISH
// BEGIN:VTIMEZONE
// TZID:America/New_York
// X-LIC-LOCATION:America/New_York
// END:VTIMEZONE
// BEGIN:VEVENT
// DESCRIPTION:Floor Coverings International In Home Consultation for good ol
//  'flow\nhttps://api.fci.cloud/app/scheduler/80466dd4-ae89-4455-93bc-c16ca3
//  694a08\ntest 1
// DTEND:20230315T233000Z
// DTSTAMP:20230317T144019Z
// DTSTART:20230315T213000Z
// SEQUENCE:0
// SUMMARY:meeting without invitation
// UID:test2@ProgramInterface.com
// END:VEVENT
// END:VCALENDAR
// ";
//         
//         var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(ical));
//         // var base64String = "QkVHSU46VkNBTEVOREFSDQpQUk9ESUQ6LS8vR29vZ2xlIEluYy8vR29vZ2xlIENhbGVuZGFyIDcwLjkwNTQvL0VODQpWRVJTSU9OOjIuMA0KQ0FMU0NBTEU6R1JFR09SSUFODQpNRVRIT0Q6UkVRVUVTVA0KQkVHSU46VlRJTUVaT05FDQpUWklEOkFtZXJpY2EvTmV3X1lvcmsNClgtTElDLUxPQ0FUSU9OOkFtZXJpY2EvTmV3X1lvcmsNCkJFR0lOOkRBWUxJR0hUDQpUWk9GRlNFVEZST006LTA1MDANClRaT0ZGU0VUVE86LTA0MDANClRaTkFNRTpFRFQNCkRUU1RBUlQ6MTk3MDAzMDhUMDIwMDAwDQpSUlVMRTpGUkVRPVlFQVJMWTtCWU1PTlRIPTM7QllEQVk9MlNVDQpFTkQ6REFZTElHSFQNCkJFR0lOOlNUQU5EQVJEDQpUWk9GRlNFVEZST006LTA0MDANClRaT0ZGU0VUVE86LTA1MDANClRaTkFNRTpFU1QNCkRUU1RBUlQ6MTk3MDExMDFUMDIwMDAwDQpSUlVMRTpGUkVRPVlFQVJMWTtCWU1PTlRIPTExO0JZREFZPTFTVQ0KRU5EOlNUQU5EQVJEDQpFTkQ6VlRJTUVaT05FDQpCRUdJTjpWRVZFTlQNCkRUU1RBUlQ7VFpJRD1BbWVyaWNhL05ld19Zb3JrOjIwMjMwMzE3VDE2MDAwMA0KRFRFTkQ7VFpJRD1BbWVyaWNhL05ld19Zb3JrOjIwMjMwMzE3VDE3MDAwMA0KRFRTVEFNUDoyMDIzMDMxN1QxNzUxMzlaDQpPUkdBTklaRVI7Q049RmVsaXBlIENyb2NoaWs6bWFpbHRvOmZlbGlwZUBjcm9jaGlrLmNvbQ0KVUlEOjJ1M3JtY2hudDBsdjJ0aG5kZ2FkN283NnAwQGdvb2dsZS5jb20NCkFUVEVOREVFO0NVVFlQRT1JTkRJVklEVUFMO1JPTEU9UkVRLVBBUlRJQ0lQQU5UO1BBUlRTVEFUPUFDQ0VQVEVEO1JTVlA9VFJVRQ0KIDtDTj1GZWxpcGUgQ3JvY2hpaztYLU5VTS1HVUVTVFM9MDptYWlsdG86ZmVsaXBlQGNyb2NoaWsuY29tDQpBVFRFTkRFRTtDVVRZUEU9SU5ESVZJRFVBTDtST0xFPVJFUS1QQVJUSUNJUEFOVDtQQVJUU1RBVD1ORUVEUy1BQ1RJT047UlNWUD0NCiBUUlVFO0NOPWZlbGlwZUBzY2hlZG9ubC5vbm1pY3Jvc29mdC5jb207WC1OVU0tR1VFU1RTPTA6bWFpbHRvOmZlbGlwZUBzY2hlZG8NCiBubC5vbm1pY3Jvc29mdC5jb20NClgtR09PR0xFLUNPTkZFUkVOQ0U6aHR0cHM6Ly9tZWV0Lmdvb2dsZS5jb20vcmdwLXR2YWEteGZtDQpYLU1JQ1JPU09GVC1DRE8tT1dORVJBUFBUSUQ6MjM1Mzk0MDQyDQpDUkVBVEVEOjIwMjMwMzE3VDE3NTEzOFoNCkRFU0NSSVBUSU9OOnRlc3RcblxuLTo6fjp+Ojp+On46fjp+On46fjp+On46fjp+On46fjp+On46fjp+On46fjp+On46fjp+On46fg0KIDp+On46fjp+On46fjp+On46fjp+On46fjo6fjp+OjotXG5Kb2luIHdpdGggR29vZ2xlIE1lZXQ6IGh0dHBzOi8vbWVldC5nb29nbA0KIGUuY29tL3JncC10dmFhLXhmbVxuT3IgZGlhbDogKElFKSArMzUzIDEgNTcxIDE2MzkgUElOOiA1ODIyNDUwMDgjXG5Nb3JlIHBobw0KIG5lIG51bWJlcnM6IGh0dHBzOi8vdGVsLm1lZXQvcmdwLXR2YWEteGZtP3Bpbj00ODEyOTI4MTAxNjE2JmhzPTdcblxuTGVhcm4gbQ0KIG9yZSBhYm91dCBNZWV0IGF0OiBodHRwczovL3N1cHBvcnQuZ29vZ2xlLmNvbS9hL3VzZXJzL2Fuc3dlci85MjgyNzIwXG5cblBsZQ0KIGFzZSBkbyBub3QgZWRpdCB0aGlzIHNlY3Rpb24uXG4tOjp+On46On46fjp+On46fjp+On46fjp+On46fjp+On46fjp+On46fjp+Og0KIH46fjp+On46fjp+On46fjp+On46fjp+On46fjp+On46fjp+Ojp+On46Oi0NCkxBU1QtTU9ESUZJRUQ6MjAyMzAzMTdUMTc1MTM4Wg0KTE9DQVRJT046DQpTRVFVRU5DRTowDQpTVEFUVVM6Q09ORklSTUVEDQpTVU1NQVJZOmdtYWlsDQpUUkFOU1A6T1BBUVVFDQpFTkQ6VkVWRU5UDQpFTkQ6VkNBTEVOREFSDQo=";
//
//         msg.AddAttachment(new SendGrid.Helpers.Mail.Attachment
//         {
//             Content = base64String,
//             ContentId = "felipe",
//             Type = "text/calendar; method=PUBLISH", // REQUEST
//             Disposition = "inline",
//             Filename = "invite.ics",
//         });
//
//         // msg.AddContent("text/plain", "Plain text content");
//         // msg.AddContent("text/html", "<html><body>html content</body></html>");
//         // msg.AddContent("text/calendar; method=PUBLISH", ical);
//
//         var result = await emailService.SendAsync(auth.APIKey, msg);
//         return Ok(result);
//     }
}