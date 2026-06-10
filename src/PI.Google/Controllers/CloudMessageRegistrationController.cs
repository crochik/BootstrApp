using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Google.Models;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.Google.Controllers;

[Authorize("default")]
[Route("/google/v1/[controller]")]
public class PushNotificationController : APIController
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public PushNotificationController(MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    [HttpPost]
    public async Task<Guid> CreateAsync([FromQuery] string token)
    {
        if (!Context.UserId.HasValue) throw new ForbiddenException();

        Guid? flowId = null;
        Guid? objectStatusId = null;
        var objectType = await _objectTypeService.GetAsync(Context, nameof(CloudMessageRegistration));
        if (objectType != null)
        {
            flowId = objectType.FlowId;
            objectStatusId = objectType.ObjectStatusId;
        }

        var registration = await _connection.Filter<CloudMessageRegistration>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, Context.UserId.Value)
            .Eq(x => x.ClientId, Context.ClientId)
            .Eq(x => x.Token, token)
            .Eq(x => x.IsActive, true)
            .Update
            .SetOnInsert(x => x.Id, Guid.NewGuid())
            .SetOnInsert(x => x.AccountId, Context.AccountId.Value)
            .SetOnInsert(x => x.EntityId, Context.UserId.Value)
            .SetOnInsert(x => x.ClientId, Context.ClientId)
            .SetOnInsert(x => x.FlowId, flowId)
            .SetOnInsert(x => x.ObjectStatusId, objectStatusId)
            .SetOnInsert(x => x.Token, token)
            .SetOnInsert(x => x.IsActive, true)
            .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, Context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync(true);

        if (registration.CreatedOn == registration.LastModifiedOn)
        {
            await _objectTypeService.FireCreateEventAsync(Context, registration);
        }

        return registration.Id;
    }

    [HttpDelete("/google/v1/[controller]({id})")]
    public async Task RemoveAsync([FromRoute] Guid id)
    {
        await _connection.Filter<CloudMessageRegistration>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .Eq(x => x.IsActive, true)
            .Update
            .Set(x => x.IsActive, false)
            .Set(x => x.LastActor, Context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateOneAsync();
    }

    [HttpDelete("/google/v1/[controller]")]
    public async Task RemoveAllAsync()
    {
        await _connection.Filter<CloudMessageRegistration>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, Context.UserId.Value)
            .Eq(x => x.ClientId, Context.ClientId)
            .Eq(x => x.IsActive, true)
            .Update
            .Set(x => x.IsActive, false)
            .Set(x => x.LastActor, Context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateManyAsync();
    }
}