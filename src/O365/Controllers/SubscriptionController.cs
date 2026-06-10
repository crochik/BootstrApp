using System;
using System.Collections.Generic;
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

public enum SubscribeToResource
{
    Default, 
    Events, 
    Messages
}

[Authorize]
[Route("/o365/v1/[controller]")]
public class SubscriptionController : APIController
{
    private readonly ILogger<SubscriptionController> _logger;
    private readonly MongoConnection _connection;
    private readonly O365Service _o365Service;

    public SubscriptionController(
        ILogger<SubscriptionController> logger,
        MongoConnection connection,
        O365Service o365Service
    )
    {
        _logger = logger;
        _connection = connection;
        _o365Service = o365Service;
    }

    [Authorize("admin")]
    [HttpPost("User({id})/{resource}")]
    public async Task<O365Subscription> SubscribeUserAsync([FromRoute] Guid id, [FromRoute] SubscribeToResource resource)
    {
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (user == null) throw NotFoundException.New<User>(id);

        var subscription = resource switch
        {
            SubscribeToResource.Messages => await _o365Service.SubscribeToMessagesAsync(user.Context),
            SubscribeToResource.Events => await _o365Service.SubscribeToEventsAsync(user.Context),
            _ => null,
        };
        
        return subscription;
    }

    [Authorize("root")]
    [HttpDelete("User({id})")]
    public async Task<IEnumerable<O365Subscription>> DeleteAsync([FromRoute] Guid id)
    {
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (user == null) throw NotFoundException.New<User>(id);

        var subscriptions = await _connection.Filter<O365Subscription>()
            .Eq(x => x.EntityId, id)
            .FindAsync();

        foreach (var subscription in subscriptions)
        {
            await _o365Service.UnsubscribeToEventsAsync(user.Context, subscription);
        }

        return subscriptions;
    }

    // [HttpPost("/o365({tenantId})/User({userId})/Subscription")]
    // public async Task<IActionResult> SubscribeToEventsAsync([FromRoute] string tenantId, [FromRoute] string userId)
    // {
    //     var result = await _o365Service.SubscribeToEventsAsync(tenantId, userId);
    //     return Ok(result);
    // }

    // [HttpGet("/o365({tenantId})/Subscription")]
    // public async Task<IActionResult> GetAsync([FromRoute] string tenantId)
    // {
    //     var result = await _o365Service.GetSubscriptionsAsync(tenantId);
    //     return Ok(result);
    // }

    // [HttpGet("/o365({tenantId})/Subscription({id})")]
    // public async Task<IActionResult> GetAsync([FromRoute] string tenantId, string id)
    // {
    //     var result = await _o365Service.GetSubscriptionAsync(tenantId, id);
    //     return Ok(result);
    // }

    // [HttpPatch("/o365({tenantId})/Subscription({id})/Renew")]
    // public async Task<IActionResult> RenewAsync([FromRoute] string tenantId, string id)
    // {
    //     var result = await _o365Service.RenewSubscriptionAsync(tenantId, id);
    //     return Ok(result);
    // }
}