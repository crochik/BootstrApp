using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models;
using Newtonsoft.Json;
using PI.Shared.Controllers;
using Services;

namespace Controllers;

[Produces("application/json")]
[Route("/verse/v1/[controller]")]
public class WebhookController : APIController
{
    private readonly ILogger<WebhookController> _logger;
    private readonly VerseService _service;

    public WebhookController(
        ILogger<WebhookController> logger,
        VerseService service
        )
    {
        this._logger = logger;
        this._service = service;
    }

    [AllowAnonymous]
    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> PostJsonAsync([FromBody] VerseEvent body)
    {
        _logger.LogInformation("Received request: {json}", JsonConvert.SerializeObject(body));
     
        await _service.ProcessAsync(body);
     
        return Ok();
    }
}