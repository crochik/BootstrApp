using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;
using EmailAddress = Microsoft.Graph.EmailAddress;

namespace Controllers;

[Route("/o365/v1/[controller]")]
public class EmailController : APIController
{
    private readonly MongoConnection _connection;
    private readonly O365Service _o365Service;

    public EmailController(MongoConnection connection, O365Service o365Service)
    {
        _connection = connection;
        _o365Service = o365Service;
    }

    [Authorize("admin")]
    [HttpPost("Draft")]
    public async Task<IActionResult> CreateDraftAsync([FromBody] CreateDraftRequest request)
    {
        var user = await _connection.Filter<PI.Shared.Models.Entity, PI.Shared.Models.User>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, request.UserId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        var identity = user?.FirstIdentity(ExternalProvider.Microsoft);

        if (identity == null) throw new NotFoundException("User");

        var message = new Message
        {
            Subject = "DRAFT: HTML Email from C# App via Graph API",
            Body = new ItemBody
            {
                ContentType = request.ContentType == "text/html" ? BodyType.Html : BodyType.Text,
                Content = request.Body,
            },
            ToRecipients =
            [
                new Recipient { EmailAddress = new EmailAddress { Address = request.To.Email, Name = request.To.Name } }
            ],
            // CcRecipients =
            // BccRecipients = 
        };

        var account = await _connection.Filter<PI.Shared.Models.Entity, PI.Shared.Models.Account>()
            .Eq(x => x.Id, user.AccountId)
            .FirstOrDefaultAsync();

        await _o365Service.AddAsync(account, identity.ExternalId, message);

        return Ok("test");
    }
}

public class CreateDraftRequest
{
    public class MailBox
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public Guid UserId { get; set; }
    public string Subject { get; set; }
    public MailBox To { get; set; }
    public MailBox[] CarbonCopy { get; set; }
    public MailBox[] BlindCarbonCopy { get; set; }
    public string Body { get; set; }
    public string ContentType { get; set; }
}