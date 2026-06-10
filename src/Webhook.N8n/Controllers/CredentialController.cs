using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Webhook.Integrations.Core.Configuration;

namespace Webhook.N8n.Controllers;

/// <summary>
/// Credential test endpoint. The n8n credential's <c>test</c> request hits
/// <c>GET /n8n/me</c> after the user enters their API key: a 200 confirms the key
/// works and the returned name labels the connection. (The API-key middleware has
/// already rejected bad keys with a 401.)
/// </summary>
[ApiController]
[Route("n8n")]
public sealed class CredentialController : ControllerBase
{
    private readonly IOptionsMonitor<IntegrationOptions> _options;

    public CredentialController(IOptionsMonitor<IntegrationOptions> options)
    {
        _options = options;
    }

    [HttpGet("me")]
    public IActionResult Me() => Ok(new { name = _options.CurrentValue.ConnectionName });
}
