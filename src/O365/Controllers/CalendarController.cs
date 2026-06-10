// using System.Threading.Tasks;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Graph;
// using Services;

// namespace Controllers
// {
//     [Authorize]
//     public class CalendarController : ControllerBase
//     {
//         private readonly IO365Client _client;

//         public CalendarController(IO365Client client)
//         {
//             this._client = client;
//         }

//         [HttpGet("/o365({tenantId})/User({userId})/Calendar")]
//         public async Task<IUserCalendarsCollectionPage> GetAsync([FromRoute] string tenantId, [FromRoute] string userId)
//         {
//             return await _client.GetCalendarsAsync(tenantId, userId);
//         }
//     }
// }
