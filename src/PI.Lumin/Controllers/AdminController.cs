using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace PI.Lumin.Controllers;

[Route("/lumin/v1/[controller]")]
public class AdminController : ControllerBase
{
    private readonly LuminService _service;

    public AdminController(
        LuminService service
        )
    {
        this._service = service;
    }

    /// <summary>
    /// Post payload to Lumnin, it will not include authorization
    /// </summary>
    [Authorize("admin")]
    [HttpPost("Send")]
    public async Task<IActionResult> SendAsync([FromBody] Payload payload)
    {
        var resp = await _service.PublishAsync(payload);
        var body = await resp.Content.ReadAsStringAsync();
        if (resp.IsSuccessStatusCode)
        {
            return Ok(new
            {
                resp.StatusCode,
                Body = body
            });
        }

        return BadRequest(new
        {
            resp.StatusCode,
            Body = body
        });
    }

    /// <summary>
    /// Generate token for lead
    /// DANGER! DANGER! DANGER!
    /// </summary>
    [Authorize("admin")]
    [HttpPost("Jwt")]
    public IActionResult Impersonate(
        [FromQuery] Guid leadId,
        [FromQuery] Guid entityId,
        [FromServices] PI.Shared.Services.AuthorizationService authorizationService
        )
    {
        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.ClientId, "Lumin"),
            new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()),
            new Claim(JwtClaimTypes.Scope, "partner"),
            new Claim(JwtClaimTypes.Scope, "scheduler"),
            new Claim("client_account_id", PI.Shared.Constants.AccountIds.FCI.ToString()),
            new Claim("pi_lead_id", leadId.ToString()),
            new Claim("pi_org_id", entityId.ToString()),
        };

        var jwt_token = authorizationService.GenerateJwtToken(claims);

        return Ok(jwt_token.Value);
    }

    /// <summary>
    /// Validate JWT token
    /// </summary>
    [HttpPost("Validate")]
    [Authorize("lumin")]
    public IActionResult Validate()
    {
        var claims = User.Claims.Select(x => new { x.Type, x.Value });
        return Ok(claims);
    }
}

