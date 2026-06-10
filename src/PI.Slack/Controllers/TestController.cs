using System.Threading.Tasks;
using Messages.Flow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers
{
    [Authorize("admin")]
    [Route("/slack/v1/[controller]")]
    public class TestController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> PostAsync(
            [FromBody] PostToSlackAction.Message post,
            [FromServices] SlackIntegrationService service
            )
        {
            await service.PostAsync(post);
            return Ok();
        }
    }
}