using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Route("/sendgrid/v1/[controller]")]
[AllowAnonymous]
public class UnsubscribeController : APIController
{
    private readonly ILogger<UnsubscribeController> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public UnsubscribeController(ILogger<UnsubscribeController> logger, MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> RedirectUnsubscribeAsync([FromRoute] Guid id)
    {
        var email = await _connection.Filter<SendGridEmailMessage>()
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (email == null) throw new NotFoundException("Invalid Url");

        if (!email.Unsubscribed.HasValue)
        {
            var unsubscribed = await _connection.Filter<SendGridEmailMessage>()
                .Eq(x => x.Id, id)
                .Update
                .Set(x => x.Unsubscribed, DateTime.UtcNow)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                // .Set(x => x.LastActor, PI.Shared.Models.Actor.Current)
                .UpdateAndGetOneAsync();

            if (unsubscribed != null)
            {
                foreach (var to in email.Message.To)
                {
                    var emailAddress = to.Email.ToLowerInvariant();

                    await _connection.Filter<SendGridEmailUnsubscribe>()
                        .Eq(x => x.Email, emailAddress)
                        .Update
                        .SetOnInsert(x => x.Email, emailAddress)
                        .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
                        .Set(x => x.SendGridEmailMessage[email.Id.ToString()], DateTime.UtcNow)
                        .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                        .Inc(x => x.Count, 1)
                        .UpdateOneAsync(true);
                }

                await UnsubscribeLeadsAsync(email);
            }
        }

        // TODO: get config from account?
        // ... 

        return Content(
            @"<html>
<head><title>Successfully Unsubscribed</title></head>
<body>
You have been successfully unsubscribed. 
</body>
", "text/html");
    }

    private async Task UnsubscribeLeadsAsync(SendGridEmailMessage message)
    {
        var context = new AccountContext(message.AccountId);

        using var cursor = _connection.Filter<Lead>()
            .Eq(x => x.AccountId, message.AccountId)
            .In(x => x.NormalizedEmail, message.Message.To.Select(x => x.Email.ToLowerInvariant()))
            .Ne(x => x.CommunicationPreferences[CommunicationChannel.Email], CommunicationPreference.OptedOut)
            .ToCursor();

        while (await cursor.MoveNextAsync())
        {
            foreach (var row in cursor.Current)
            {
                var lead = await _connection.Filter<Lead>()
                    .Eq(x => x.AccountId, message.AccountId)
                    .Eq(x => x.Id, row.Id)
                    .Ne(x => x.CommunicationPreferences[CommunicationChannel.Email], CommunicationPreference.OptedOut)
                    .Update
                    .Set(x => x.CommunicationPreferences[CommunicationChannel.Email], CommunicationPreference.OptedOut)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    // .Set(x => x.LastActor, PI.Shared.Models.Actor.Current)
                    .UpdateAndGetOneAsync();

                if (lead == null) continue;

                _logger.LogInformation("Unsubscribed {LeadId} with request from {SendGridEmailMessageId}", lead.Id, message.Id);

                await _objectTypeService.FireObjectUpdatedAsync(
                    context,
                    lead,
                    new Dictionary<string, object>
                    {
                        { $"{nameof(Lead.CommunicationPreferences)}|{nameof(CommunicationChannel.Email)}", CommunicationPreference.OptedOut },
                    },
                    e =>
                    {
                        e.Description = "Contact unsubscribed from receiving emails";
                        e.SetMetaValue(nameof(Lead.NormalizedEmail), lead.NormalizedEmail);
                        e.SetRefValue(nameof(SendGridEmailMessage), message.Id);
                    }
                );
            }
        }
    }
}