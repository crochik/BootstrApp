// using System.Threading.Tasks;
// using Crochik.Mongo;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using PI.Salesforce.IIF;
// using PI.Shared.Controllers;
// using PI.Shared.Exceptions;
//
// namespace Controllers;
//
// [AllowAnonymous]
// [Route("/salesforce/v1/[controller]")]
// public class QuoteController : APIController
// {
//     private readonly MongoConnection _connection;
//
//     public QuoteController(MongoConnection connection)
//     {
//         _connection = connection;
//     }
//
//     // TODO: get rid of me or limit access
//     // ...
//     [HttpGet("/salesforce/v1/[controller]({id})/IIF")]
//     public async Task<IActionResult> GenerateIifAsync([FromRoute] string id, [FromQuery] string secret)
//     {
//         if (secret != "TheCheeseFactory") throw new ForbiddenException();
//         var generator = new QbProposalGenerator(_connection);
//         var content = await generator.GenerateAsync(id);
//         return Content(content);
//     }
// }
