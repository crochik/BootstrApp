using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Models;
using Zapier.Models;

namespace Zapier.Controllers;

[Authorize("zapier")]
[Route("/zapier/v1/[controller]")]
public class SubscriptionController : APIController
{
    private readonly ILogger<SubscriptionController> _logger;
    private readonly MongoConnection _connection;

    public SubscriptionController(ILogger<SubscriptionController> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    [HttpPost("ObjectType/{objectTypeName}")]
    [HttpPost("ObjectType")]
    public async Task<Subscription> SubscribeAsync([FromRoute] string objectTypeName, [FromBody] SubscriptionRequest request)
    {
        objectTypeName ??= request.ObjectType;

        var keys = request.Events ?? new[] { request.Event };

        // delete any subscription for the same trigger (probably will never happen)
        await _connection.Filter<Subscription>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, Context.UserId)
            .Eq(x => x.Url, request.HookUrl)
            .DeleteAsync();

        var subscription = new Subscription
        {
            AccountId = Context.AccountId.Value,
            EntityId = Context.UserId.Value,
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.UtcNow,
            LastActor = Context.Actor(),
            Name = $"{objectTypeName}: {string.Join(", ", keys)}",
            ObjectType = objectTypeName,
            Keys = keys,
            Url = request.HookUrl,
            OrganizationId = Context.OrganizationId,
            ProfileId = Context.ProfileId.Value,
            ClientId = Context.ClientId
        };

        await _connection.InsertAsync(subscription);

        _logger.LogInformation("Created Subscription for {ObjectType} to {Url} for {EntityId}", subscription.ObjectType, subscription.Url, subscription.EntityId);

        return subscription;
    }

    [HttpDelete("ObjectType/{objectTypeName}")]
    [HttpDelete("ObjectType")]
    public async Task<IActionResult> UnsubscribeAsync([FromRoute] string objectTypeName, [FromBody] SubscriptionRequest request)
    {
        objectTypeName ??= request.ObjectType;

        var entityId = Context.OrganizationId ?? Context.AccountId.Value;
        var keys = request.Events ?? new[] { request.Event };

        var found = await _connection.Filter<Subscription>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, entityId)
            .Eq(x => x.Url, request.HookUrl)
            .DeleteAsync();

        if (found > 0)
        {
            _logger.LogInformation("Removed Subscription for {ObjectType} to {Url} already exists for {EntityId}", objectTypeName, request.HookUrl, entityId);
            return Ok();
        }

        _logger.LogError("Did not find Subscription for {ObjectType} to {Url} for {EntityId}", objectTypeName, request.HookUrl, entityId);

        return NotFound("Subscription");
    }
}