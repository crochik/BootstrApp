using System;
using System.Net.Http;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Authorize("default")]
[Produces("application/json")]
[Route("/api/v1/[controller]")]
public class IntegrationController : APIController
{
    private readonly MongoConnection _connection;
    private readonly IntegrationAuthService _authService;

    public IntegrationController(MongoConnection connection, IntegrationAuthService authService)
    {
        _connection = connection;
        _authService = authService;
    }

    [Authorize("managerplus")]
    [HttpGet("DataForm")]
    public async Task<Form> EditIntegrationDataFormAsync()
    {
        // Login ("add")
        return new Form
        {
            Name = "Integration",
            Title = "Integration",
            Fields = new FormField[]
            {
                new ReferenceField
                {
                    Name = "IntegrationId",
                    ReferenceFieldOptions = new ReferenceFieldOptions
                    {
                        ObjectType = "Integration",
                    }
                },
                new LabelField
                {
                    Name = "Message",
                    Label = "Connect your account so we can exchange information between the two systems",
                },
                new LabelField
                {
                    Name = "Instructions",
                    Label = "After clicking Start we will open a new tab for you to continue the process",
                },
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Login",
                    Action = "Login",
                },
            }
        };
    }

    [Authorize("managerplus")]
    [HttpPost("DataForm")]
    public async Task<DataFormActionResponse> LoginAsync([FromBody] DataFormActionRequest request)
    {
        if (!request.TryGetGuidParam("IntegrationId", out var integrationId))
        {
            return DataFormActionResponse.Error(request, "Missing IntegrationId");    
        }
        
        var result = await _authService.GetLoginUrlAsync(Context, integrationId);
        if (!result.IsSuccess) return DataFormActionResponse.Error(request, result.Status);
        
        return new DataFormActionResponse(request, "Launching on new Browser tab")
        {
            NextUrl = result.Value,
            Success = true,
        };
    }

    [Authorize("managerplus")]
    [HttpPost("{integration}/DataForm")]
    public async Task<DataFormActionResponse> LoginAsync([FromRoute] string integration, [FromBody] DataFormActionRequest request)
    {
        var result = await _authService.GetLoginUrlAsync(Context, integration);
        if (!result.IsSuccess) return DataFormActionResponse.Error(request, result.Status);
        
        return new DataFormActionResponse(request, "Launching on new Browser tab")
        {
            NextUrl = result.Value,
            Success = true,
        };
    }
    
    [HttpGet("{integration}/redirect")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginCallbackAsync([FromRoute] string integration)
    {
        // redirection from installing github app
        // http://localhost:5002/api/v1/integration/GitHub/redirect?code=ea2b8ded335c14491e27&installation_id=57486635&setup_action=install
        
        var result = await _authService.RedirectFromLoginAsync(integration, Request.Query);
        if (!result.IsSuccess) return BadRequest(result.Status);
        
        return Ok("It Worked!");
    }
}