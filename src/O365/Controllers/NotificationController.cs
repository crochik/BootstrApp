using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Models;
using Services;

namespace Controllers;

/// <summary>
/// Handle webhook calls from O365
/// </summary>
[AllowAnonymous]
[Route("/o365/v1/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly ILogger<NotificationController> _logger;
    private readonly O365CalendarService _calendarService;

    public NotificationController(
        ILogger<NotificationController> logger,
        O365CalendarService calendarService
    )
    {
        _logger = logger;
        _calendarService = calendarService;
    }

    [HttpPost("{accountId}")]
    public async Task<IActionResult> PostAsync(Guid accountId, [FromQuery] string validationToken)
    {
        if (Request.ContentType.StartsWith("text/plain") && !string.IsNullOrEmpty(validationToken))
        {
            return ConfirmSubscription(validationToken);
        }

        if (Request.ContentType.StartsWith("application/json"))
        {
            return await ProcessNotificationAsync(accountId);
        }

        // error
        var body = Request.GetBody();
        _logger.LogError("{contentType} {query} {body}", Request.ContentType, Request.QueryString, body);
        return BadRequest();
    }

    [HttpPost("{accountId}/{userId}")]
    public async Task<IActionResult> PostAsync(Guid accountId, Guid userId, [FromQuery] string validationToken)
    {
        if (Request.ContentType.StartsWith("text/plain") && !string.IsNullOrEmpty(validationToken))
        {
            return ConfirmSubscription(validationToken);
        }

        if (Request.ContentType.StartsWith("application/json"))
        {
            return await ProcessNotificationAsync(accountId, userId);
        }

        // error
        var body = Request.GetBody();
        _logger.LogError("{contentType} {query} {body}", Request.ContentType, Request.QueryString, body);
        return BadRequest();
    }

    private async Task<IActionResult> ProcessNotificationAsync(Guid accountId, Guid? userId = null)
    {
        var body = Request.GetBody();
        _logger.LogDebug("{query} {contentType}: {body}", Request.QueryString, Request.ContentType, body);

        var payload = JsonConvert.DeserializeObject<O365GraphPayload>(body);
        var notifications = payload.Value.Select(x => new O365EventNotification
        {
            AccountId = accountId,
            UserId = userId,
            SubscriptionId = x.SubscriptionId,
            ChangeType = x.ChangeType,
            Resource = x.Resource
        });

        await _calendarService.PublishAsync(notifications);

        return Accepted();
    }

    private IActionResult ConfirmSubscription(string validationToken)
    {
        _logger.LogInformation("Register: {query}", Request.QueryString);
        return Content(validationToken, "text/plain");
    }
}