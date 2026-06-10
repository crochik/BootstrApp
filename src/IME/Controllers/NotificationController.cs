using System;
using System.Threading.Tasks;
using Crochik.Messaging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using Services;

namespace Controllers;

[Route("/ime/v1/[controller]")]
[Produces("application/json")]
public class NotificationController : APIController
{
    private readonly MongoConnection _connection;
    private readonly IMessageBroker _messageBroker;

    public NotificationController(
        MongoConnection connection,
        IMessageBroker messageBroker
        )
    {
        _connection = connection;
        _messageBroker = messageBroker;
    }

    [HttpPost]
    [Authorize("partner")]
    public async Task<IActionResult> NotificationAsync([FromBody] NotificationPayload payload)
    {
        var notification = new Notification
        {
            AccountId = Context.AccountId.Value,
            Payload = payload,
            CreatedOn = DateTime.UtcNow,
        };

        await _connection.InsertAsync(notification);

        await _messageBroker.PublishAsync(notification.BuildRoute(), notification);

        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("{id}")]
    // [Authorize("partner")]
    public async Task<IActionResult> ReplayAsync(Guid id, [FromServices] NotificationProcessor processor)
    {
        var notification = await _connection.Filter<Notification>()
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (notification == null) throw new NotFoundException(nameof(Notification), id);

        // await _messageBroker.PublishAsync(notification.BuildRoute(), notification);
        await processor.ProcessAsync(notification);

        return Ok();
    }
}
