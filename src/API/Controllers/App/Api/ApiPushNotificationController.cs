using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Google.Models;
using PI.Shared.Attributes;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Authorize("rest")]
[Route("/app/api/PushNotification")]
[ApiExplorerSettings(GroupName = "rest")]
public class ApiPushNotificationController : APIController
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public ApiPushNotificationController(MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    [HttpPut]
    [UseApiNames]
    public async Task<RegistrationResponse> CreateAsync([FromQuery] string token)
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

        var now = DateTime.UtcNow;
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
            .SetOnInsert(x => x.CreatedOn, now)
            .Set(x => x.LastActor, Context.Actor())
            .Set(x => x.LastModifiedOn, now)
            .UpdateAndGetOneAsync(true);

        if (registration.CreatedOn == registration.LastModifiedOn)
        {
            await _objectTypeService.FireCreateEventAsync(Context, registration);
        }
        
        return new RegistrationResponse
        {
            RegistrationId = registration.Id,
        };
    }

    [HttpDelete("/app/api/PushNotification({id})")]
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

    [HttpDelete]
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

    public class RegistrationResponse
    {
        [ApiName("registrationId")]
        public Guid RegistrationId { get; set; }
    }
}

