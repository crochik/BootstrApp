using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Webhook.Integrations.Core.Configuration;

namespace Webhook.Zapier.Controllers;

/// <summary>
/// Connection test endpoint. Zapier calls <c>GET /zapier/me</c> after the user enters
/// their API key: a 200 confirms the key works and the returned name labels the
/// connection. (The API-key middleware has already rejected bad keys with a 401.)
/// </summary>
[ApiController]
[Route("zapier")]
public sealed class AuthController : ControllerBase
{
    private readonly IOptionsMonitor<IntegrationOptions> _options;

    public AuthController(IOptionsMonitor<IntegrationOptions> options)
    {
        _options = options;
    }

    [HttpGet("me")]
    public IActionResult Me() => Ok(new { name = _options.CurrentValue.ConnectionName });
}
