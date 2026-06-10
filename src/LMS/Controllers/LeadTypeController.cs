using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using LMS.Models;
using Messages.Flow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;
using PI.Shared.Services.ActionRunners;

namespace LMS.Controllers;

[Authorize("admin")]
[Route("/lms/v1/[controller]")]
public class LeadTypeController : APIController
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public LeadTypeController(MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    [HttpPost("LMSTransaction({id})")]
    public async Task<string[]> ReprocessAsync([FromRoute] Guid id, [FromServices] ActionRunnerService service)
    {
        var transaction = await _connection.Filter<Transaction>()
            // .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (transaction == null) throw NotFoundException.New<Transaction>(id);

        var leadType = await _connection.Filter<LeadType>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, transaction.Request.LeadTypeId)
            .FirstOrDefaultAsync();

        if (leadType == null) throw NotFoundException.New<LeadType>(id);

        transaction = await _connection.Filter<Transaction>()
            .Eq(x => x.Id, transaction.Id)
            .Update
            .Set(x => x.Tags, new[] { "LMS" })
            .Set(x => x.FlowId, leadType.TransactionFlowId)
            .Set(x => x.ObjectStatusId, leadType.TransactionObjectStatusId)
            .UpdateAndGetOneAsync();
        
        var initialEvent = new GenericFlowEvent
        {
            AccountId = Context.AccountId.Value,
            ObjectType = Transaction.ObjectTypeName,
            TargetId = transaction.Id,
            EventTypeId = EventIds.OnStatusEntered,
            FlowId = transaction.FlowId.Value,
            StatusId = transaction.ObjectStatusId.Value,
        };

        var entityContext = new AccountContext(Context.AccountId.Value);
        await service.RunAsync(entityContext, initialEvent);

        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.Id, initialEvent.RunId)
            .IncludeFields(x => x.Id, x => x.Steps)
            .FirstOrDefaultAsync();

        var log = flowRun.Steps
            .Select(x => string.IsNullOrWhiteSpace(x.Event.Action) ? x.Event.Description : $"{x.Event.Action}: {x.Event.Description}")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        transaction = await _connection.Filter<Transaction>()
            .Eq(x => x.Id, transaction.Id)
            .Update
            .Set(x => x.Message, string.Join('\n', log))
            .Set(x => x.Response.FinishedOn, DateTime.UtcNow)
            .Set(x => x.Refs[nameof(FlowRun)], initialEvent.RunId)
            .UpdateAndGetOneAsync();

        return log;
    }

    // /// <summary>
    // /// action to create new flow for lead type
    // /// Create flow
    // /// </summary>
    // [HttpPost("Flow/DataViewAction")]
    // [HttpPost("Flow/DataForm")]
    // public async Task<DataFormActionResponse> NewFlowDataFormActionAsync([FromBody] DataFormActionRequest request)
    // {
    //     if (!request.TryGetGuidParam("LeadTypeId", out var id))
    //     {
    //         return new DataFormActionResponse(request, "Missing required parameter: LeadTypeId");
    //     }
    //
    //     var leadType = await _connection.Filter<LeadType>()
    //         .Eq(x => x.AccountId, Context.AccountId)
    //         .Eq(x => x.Id, id)
    //         .FirstOrDefaultAsync();
    //
    //     if (leadType == null) throw NotFoundException.New<LeadType>(id);
    //
    //     if (!request.TryGetParam("CanReject", out var canRejectObj))
    //     {
    //         return new DataFormActionResponse(request, "Missing required parameter: CanReject");
    //     }
    //
    //     var canReject = canRejectObj switch
    //     {
    //         bool b => b,
    //         string str => bool.TryParse(str, out var b) ? b : false,
    //         _ => false,
    //     };
    //
    //     if (!request.TryGetStrParam("ContentType", out var contentType))
    //     {
    //         return new DataFormActionResponse(request, "Missing required parameter: ContentType");
    //     }
    //
    //     var builder = new LeadTypeFlowBuilder(leadType, canReject);
    //     var flow = builder.Build();
    //     
    //     if (flow == null)
    //     {
    //         return new DataFormActionResponse(request, "Failed to create flow");
    //     }
    //
    //     await _connection.InsertAsync(flow);
    //
    //     return new DataFormActionResponse(request)
    //     {
    //         Message = $"Flow created for {leadType.Name}",
    //         Success = true,
    //         NextUrl = $"page:/Flow?id={flow.Id}",
    //     };
    // }

    // [HttpGet("/lms/v1/[controller]({leadTypeId})/Flow")]
    // public async Task<Flow> BuildFlowAsync([FromRoute] Guid leadTypeId)
    // {
    //     var leadType = await _connection.Filter<LeadType>()
    //         .Eq(x => x.AccountId, Context.AccountId)
    //         .Eq(x => x.Id, leadTypeId)
    //         .FirstOrDefaultAsync();
    //
    //     
    //     var builder = new LeadTypeFlowBuilder(leadType, true);
    //     return builder.Build();
    // }
}