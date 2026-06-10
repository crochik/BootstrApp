using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.SingleUseTickets;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Authorize("default")]
[Produces("application/json")]
[Route("/openapi/v1/[controller]")]
public class IntegrationController : APIController
{
    private const string APP_NAME = "programinterface-com";

    private readonly IntegrationAuthService _authService;
    private readonly MongoConnection _connection;

    public IntegrationController(
        IntegrationAuthService authService,
        MongoConnection connection
    )
    {
        _authService = authService;
        _connection = connection;
    }

    [Authorize("managerplus")]
    [HttpGet("GitHub/DataForm")]
    public Form EditIntegrationDataFormAsync()
    {
        // TODO: check if there is an existing one
        // ...

        // Login ("add")
        return new Form
        {
            Name = "Integration",
            Title = "Integration",
            Fields = new FormField[]
            {
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
    [HttpPost("GitHub/DataForm")]
    public async Task<DataFormActionResponse> LoginAsync([FromBody] DataFormActionRequest request)
    {
        var result = await GetLoginUrlAsync(Context, IntegrationIds.GitHub);
        if (!result.IsSuccess) return DataFormActionResponse.Error(request, result.Status);

        return new DataFormActionResponse(request, "Launching on new Browser tab")
        {
            NextUrl = result.Value,
            Success = true,
        };
    }

    private async Task<Result<string>> GetLoginUrlAsync(IEntityContext context, Guid integrationId)
    {
        var integration = IntegrationIds.GetName(integrationId);
        var ticket = await _connection.InsertAsync(new IntegrationSingleUseTicket
        {
            Id = Guid.NewGuid(),
            AccountId = context.AccountId.Value,
            EntityId = context.OrganizationId ?? context.AccountId.Value,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            Name = $"Add {integration} integration",
            ExpiresOn = DateTime.UtcNow.AddMinutes(10),
            IsActive = true,
            IntegrationId = integrationId,
        });

        var options = await _authService.GetOAuthOptionsAsync(integrationId);
        if (!options.IsSuccess) return options.ConvertTo<string>();

        var url = $"https://github.com/apps/{APP_NAME}/installations/new?state={ticket.Id}";

        return Result.Success(url);
    }

    [HttpGet("GitHub/redirect")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginCallbackAsync()
    {
        var result = await _authService.RedirectFromLoginAsync(IntegrationIds.GitHub, Request.Query);
        if (!result.IsSuccess) return BadRequest(result.Status);

        return Ok("It Worked!");
    }
}