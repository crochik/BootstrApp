using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace PI.Shared.Controllers;

public abstract class AbstractUserActionController : APIController
{
    protected readonly ILogger<AbstractUserActionController> _logger;
    protected readonly MongoConnection _connection;
    protected readonly UserActionService _service;
    protected readonly ObjectTypeService _objectTypeService;

    protected AbstractUserActionController(
        ILogger<AbstractUserActionController> logger,
        MongoConnection connection,
        UserActionService service,
        ObjectTypeService objectTypeService
    )
    {
        _logger = logger;
        _connection = connection;
        _service = service;
        _objectTypeService = objectTypeService;
    }
    
    protected async Task<PI.Shared.Form.Models.Form> BuildActionFormForEventAsync(string objectTypeName, Guid eventId)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        var action = await _objectTypeService.GetUserActionUsingObjectTypeAsync(Context, objectType, eventId, null);
        if (action == null) throw new NotFoundException("Event");

        return await _service.BuildUserActionFormAsync(Context, action, BuildRunContext());
    }
    
    protected async Task<DataFormActionResponse> RunUserActionAsync(string objectTypeName, Guid objectId, Guid eventId, Guid flowRunId, DataFormActionRequest request)
    {
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, flowRunId)
            .Eq(x => x.ObjectType, objectTypeName)
            .FirstOrDefaultAsync();

        if (flowRun?.InitialEvent?.TargetId != objectId)
        {
            throw new NotFoundException("FlowRun");
        }

        request.SelectedIds = new[]
        {
            objectId,
        };

        var result = await _service.ExecuteAsync(Context, objectTypeName, eventId, request, flowRun);

        return result;
    }

    /// <summary>
    /// Run custom action (custom request and response payload)
    /// </summary>
    protected async Task<IResult> ExecuteCustomActionAsync(string objectTypeName, Guid id, Guid eventId, IDictionary<string, object> request, Channel<IResult> channel=null, CancellationToken ct=default)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        var eventType = await _objectTypeService.GetUserActionUsingObjectTypeAsync(Context, objectType, eventId, null);

        if (eventType?.Trigger is not UserTrigger trigger)
        {
            _logger.LogError("Invalid User Action: {EventId}", eventId);
            return Result.Error($"Invalid User Action for {objectType.Label ?? objectType.Name}");
        }        
        
        return await _service.ExecuteCustomActionAsync(Context, objectType, id, eventType, request, channel, ct, null);
    }
    
    protected async Task<DataFormActionResponse> RunUserActionAsync(string objectTypeName, Guid eventId, DataFormActionRequest request)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        var eventType = await _objectTypeService.GetUserActionUsingObjectTypeAsync(Context, objectType, eventId, null);

        if (eventType?.Trigger is not UserTrigger trigger)
        {
            _logger.LogError("Invalid User Action: {EventId}", eventId);
            return new DataFormActionResponse(request)
            {
                Message = $"Invalid User Action for {objectType.Label ?? objectType.Name}",
            };
        }

        if ((request.SelectedIds == null || request.SelectedIds.Length < 1) && string.IsNullOrEmpty(request.View))
        {
            // infer selectedId from context
            var selectedId = default(Guid?);
            switch (objectType.FullName)
            {
                case nameof(Account):
                    selectedId = Context.Role switch
                    {
                        EntityRoleId.Account => Context.AccountId,
                        EntityRoleId.Admin => Context.AccountId,
                        _ => throw new ForbiddenException(Context)
                    };
                    break;

                case nameof(Entity):
                    selectedId = Context.Role switch
                    {
                        EntityRoleId.Admin => Context.AccountId,
                        EntityRoleId.Organization => Context.OrganizationId,
                        EntityRoleId.User => Context.UserId,
                        _ => throw new ForbiddenException(Context)
                    };
                    break;

                case nameof(Organization):
                    selectedId = Context.Role switch
                    {
                        EntityRoleId.Organization => Context.OrganizationId,
                        EntityRoleId.Manager => Context.OrganizationId,
                        _ => throw new ForbiddenException(Context)
                    };
                    break;

                case nameof(User):
                    selectedId = Context.Role switch
                    {
                        EntityRoleId.Admin => Context.UserId,
                        EntityRoleId.Manager => Context.UserId,
                        EntityRoleId.User => Context.UserId,
                        _ => throw new ForbiddenException(Context)
                    };
                    break;
            }

            if (!selectedId.HasValue) return new DataFormActionResponse(request, "Missing required id(s)");

            request.SelectedIds = [selectedId.Value];
        }

        var result = await _service.ExecuteAsync(Context, objectType, eventType, request);

        return result;
    }

    /// <summary>
    /// Get Action Form for ongoing flow run
    /// </summary>
    protected async Task<PI.Shared.Form.Models.Form> BuildActionFormForFlowRunAsync(string objectTypeName, Guid objectId, Guid eventId, Guid flowRunId)
    {
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, flowRunId)
            .Eq(x => x.ObjectType, objectTypeName)
            .FirstOrDefaultAsync();

        if (flowRun?.InitialEvent?.TargetId != objectId)
        {
            throw new NotFoundException("FlowRun");
        }

        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException("ObjectType");
        var obj = await _objectTypeService.GetFlatObjectAsync(Context, objectType, objectId);
        if (obj == null) throw new NotFoundException(objectTypeName, objectId);

        var objectStatusId = obj.GetOptionalGuid(nameof(FlowObjectModel.ObjectStatusId));
        var action = await _objectTypeService.GetUserActionUsingObjectTypeAsync(Context, objectType, eventId, objectStatusId);
        if (action == null) throw new NotFoundException("Event");

        var runContext = flowRun.BuildHandlebarsContext();
        
        // add request parameters if any
        if (Request.Query.Count > 0)
        {
            var req = new Dictionary<string, object>();
            foreach (var query in Request.Query)
            {
                req.Add(query.Key, query.Value.Count == 1 ? query.Value[0] : query.Value.ToArray());
            }

            runContext.TryAdd("Request|Parameters", req);
        }

        return await _service.BuildUserActionFormAsync(Context, action, runContext);
    }
}