using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.CompanyCam.Services;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Requests;

namespace PI.CompanyCam.Controllers;

[Produces("application/json")]
[Route("/companycam/v1/[controller]")]
public class IntegrationController : APIController
{
    private readonly CompanyCamService _service;

    public IntegrationController(CompanyCamService service)
    {
        _service = service;
    }

    [Authorize("manager")]
    [HttpGet("DataForm")]
    public async Task<Form> EditIntegrationDataFormAsync()
    {
        return await _service.GetAddOrEditFormAsync(Context);
    }

    [HttpPost("DataForm")]
    [Authorize("manager")]
    public async Task<DataFormActionResponse> EditIntegrationDataFormAsync([FromBody] DataFormActionRequest request)
    {
        return await _service.AddOrEditFormAsync(Context, request);
    }

    [HttpGet("redirect")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(
        [FromQuery] string state,
        [FromQuery] string code
    )
    {
        if (!Guid.TryParse(state, out var ticketId)) throw new BadRequestException("Invalid state");

        var result = await _service.LoginRedirectAsync(ticketId, code);
        if (!result.IsSuccess) throw new Exception(result.Status);

        return Ok("CompanyCam integration added to Account. You can close this tab");
    }
}