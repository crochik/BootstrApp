using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Services;

namespace IDP.Controllers;

[Route("[controller]/[action]")]
public class PasswordlessController(
    ILogger<PasswordlessController> logger,
    PasswordlessService service) : ControllerBase
{
    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Start([FromBody] PasswordlessService.StartRequest req)
    {
        if (req == null) return BadRequest(new { error = "invalid_request" });

        logger.LogInformation("Passwordless start for {ClientId}", req.ClientId);

        var result = await service.RequestPinAsync(req);

        // do not return unknown status
        return result.IsError ? BadRequest(new { error = result.Status }) : Ok();
    }
}
