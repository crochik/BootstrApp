using System;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Notifications;

namespace Controllers;

[Route("/api/v1/[controller]")]
public class NotificationController : APIController
{
    private readonly ILogger<NotificationController> _logger;
    private readonly MongoConnection _connection;

    public NotificationController(ILogger<NotificationController> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    [HttpGet("/api/v1/[controller]({id})")]
    public async Task<IActionResult> RedirectAsync(Guid id)
    {
        using var scope = _logger.AddScope(new
        {
            NotificationId = id,
        });

        _logger.LogInformation("Redirect to open notification url");

        var notification = await _connection.Filter<Notification>()
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (notification == null) throw new NotFoundException("Notification Not Found");

        if (!notification.ReadOn.HasValue)
        {
            _logger.LogInformation("Mark notification read");

            var updated = await _connection.Filter<Notification>()
                .Eq(x => x.Id, id)
                .Eq(x => x.ReadOn, null)
                .Update
                .Set(x => x.ReadOn, DateTime.UtcNow)
                .UpdateAndGetOneAsync();

            notification = updated ?? notification;
        }

        return Redirect(notification.Url);
    }

    [Authorize("default")]
    [HttpPost("{id}")]
    [HttpPost("/api/v1/[controller]({id})")]
    public async Task<Notification> MarkAsReadAsync(Guid id, bool unread)
    {
        using var scope = _logger.AddScope(new
        {
            NotificationId = id,
        });

        _logger.LogInformation("Redirect to open notification url");

        var notification = await _connection.Filter<Notification>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (notification == null) throw new NotFoundException("Notification Not Found");

        switch (Context.Role)
        {
            case EntityRoleId.Admin:
                break;
            case EntityRoleId.Manager:
                if (notification.EntityId != Context.OrganizationId.Value && notification.EntityId != Context.UserId.Value) throw new ForbiddenException(Context);
                break;
            case EntityRoleId.User:
            case EntityRoleId.Profile:
                if (notification.EntityId != Context.UserId.Value) throw new ForbiddenException(Context);
                break;
            default:
                throw new ForbiddenException(Context);
        }

        if (!notification.ReadOn.HasValue)
        {
            var updated = await _connection.Filter<Notification>()
                .Eq(x => x.Id, id)
                .Eq(x => x.ReadOn, null)
                .Update
                .Set(x => x.ReadOn, DateTime.UtcNow)
                .Set(x=>x.LastModifiedOn, DateTime.UtcNow)
                .Set(x=>x.LastActor, Context.Actor())
                .UpdateAndGetOneAsync();
           
            notification = updated ?? notification;
        }

        return notification;
    }
}