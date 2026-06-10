using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.SingleUseTickets;
using PI.Shared.Requests;
using PI.Shared.Services;
using PI.Typeform.Services;

namespace PI.Typeform.Controllers;

[Produces("application/json")]
[Route("/typeform/v1/[controller]")]
public class IntegrationController : APIController
{
    private readonly MongoConnection _connection;
    private readonly TypeformClient _client;

    public IntegrationController(MongoConnection connection, TypeformClient client)
    {
        _connection = connection;
        _client = client;
    }

    [Authorize("admin")]
    [HttpGet("DataForm")]
    public async Task<Form> GetLoginDataFormAsync()
    {
        // TODO: check whether there is already an integration or not
        // ...

        return new Form
        {
            Name = "Typeform",
            Title = "Typeform.com",
            Fields = new FormField[]
            {
                new LabelField
                {
                    Name = "Message",
                    Label = "Authenticate with your typeform.com user so you can access your forms and responses",
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
                    Name = "Start",
                    Action = "Login",
                },
            }
        };
    }

    [HttpPost("DataForm")]
    [Authorize("admin")]
    public async Task<DataFormActionResponse> ScheduleAppointmentAsync([FromBody] DataFormActionRequest request)
    {
        var ticket = await _connection.InsertAsync(new SingleUseTicket
        {
            Id = Guid.NewGuid(),
            AccountId = Context.AccountId.Value,
            EntityId = Context.AccountId.Value,
            CreatedOn = DateTime.UtcNow,
            LastActor = Context.Actor,
            Name = "Add Typeform integration",
            ExpiresOn = DateTime.UtcNow.AddMinutes(10),
            IsActive = true,
        });

        return new DataFormActionResponse(request, "Launching on new Browser tab")
        {
            NextUrl = _client.GetUrl(ticket.Id.ToString()),
            Success = true,
        };
    }

    [HttpGet("redirect")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(
        [FromQuery] string state,
        [FromQuery] string code,
        [FromServices] ObjectTypeService objectTypeService
    )
    {
        if (!Guid.TryParse(state, out var ticketId)) throw new BadRequestException("Invalid state");

        var ticket = await _connection.Filter<SingleUseTicket>()
            .Eq(x => x.Id, ticketId)
            .Ne(x => x.IsActive, false)
            .Gt(x => x.ExpiresOn, DateTime.UtcNow)
            .Update
            .Set(x => x.IsActive, false)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        if (ticket == null) throw new BadRequestException("Expired Session");

        var token = await _client.GetTokenAsync(code);
        var user = await _client.GetUserAsync(token.AccessToken);

        var context = new AccountContext(ticket.AccountId);
        var objectType = await objectTypeService.GetAsync(context, nameof(TypeformIntegrationConfiguration));
        if (objectType == null) throw NotFoundException.New(nameof(TypeformIntegrationConfiguration));
        
        var result = await objectTypeService.AddObjectAsync(
            context,
            objectType,
            new TypeformIntegrationConfiguration
            {
                Token = token,
                UserId = user.UserId,
                Email = user.Email,
                Alias = user.Alias,
                Description = $"{user.Alias} ({user.Email}) @ Typeform.com",
                Name = "Typeform.com",
            },
            new ObjectTypeService.AddObjectOptions
            {
                IsUpsert = true
            }
        );

        if (!result) throw new Exception(result.Status);

        return Ok("Typeform integration added to Account. You can close this tab");
    }
}