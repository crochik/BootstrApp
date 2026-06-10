using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Controllers.Models;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using Services;
using Flow = PI.Shared.Models.Flow;

namespace Controllers;

[Authorize("default")]
[Route("/api/v1/[controller]")]
public class FlowActionsController : APIController // AbstractNewModelController<IFlowActionAdapter, IFlowAction, Models.FlowAction>
{
    private readonly IMapper _mapper;
    private readonly FlowService _flowService;

    public FlowActionsController(
        IMapper mapper,
        FlowService flowService//,
    )
    {
        _mapper = mapper;
        _flowService = flowService;
    }

    [HttpGet("/api/v1/[controller]")]
    [ProducesResponseType(typeof(IEnumerable<FlowAction>), 200)]
    public IActionResult Get()
    {
        // TODO: fow now, get for any flowtype (there is only one right now :))
        var rows = _flowService.GetForFlowType(Context, null);
        var result = rows?.Select(x =>
        {
            var action = _mapper.Map<FlowAction>(x);
            action.AccountId = Context.AccountId.Value;
            action.EntityId = Context.AccountId.Value;
            return action;
        });

        return Ok(result);
    }

    [HttpGet("/api/v1/Flow({flowId})/EventType({eventTypeId})/[controller]/DataForm")]
    public async Task<IEnumerable<FlowAction>> GetFlowActionsForEventTypeAsync(
        [FromRoute] Guid flowId,
        [FromRoute] Guid eventTypeId,
        [FromServices] IEventTypeAdapter eventTypeAdapter,
        [FromServices] MongoConnection connection)
    {
        var eventType = await eventTypeAdapter.GetByIdAsync(eventTypeId);
        if (eventType == null) throw new NotFoundException(nameof(EventType), eventTypeId);

        var flow = await connection.Filter<Flow>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .FirstOrDefaultAsync();
        if (flow == null) throw new NotFoundException(nameof(Flow), flowId);

        var rows = _flowService.GetActions(Context, flow, eventType);
        var result = rows?.Select(x =>
        {
            var action = _mapper.Map<FlowAction>(x);
            action.AccountId = Context.AccountId.Value;
            action.EntityId = Context.AccountId.Value;
            return action;
        });

        return result;
    }

    [Obsolete]
    [HttpGet("/api/v1/EventType({id})/[controller]")]
    [ProducesResponseType(typeof(IEnumerable<FlowAction>), 200)]
    public async Task<IActionResult> GetActionsForEventTypeAsync(
        [FromRoute] Guid id,
        [FromServices] IEventTypeAdapter eventTypeAdapter)
    {
        var eventType = await eventTypeAdapter.GetByIdAsync(id);
        if (eventType == null) return NotFound();

        var rows = _flowService.GetActions(Context, null, eventType);
        var result = rows?.Select(x =>
        {
            var action = _mapper.Map<FlowAction>(x);
            action.AccountId = Context.AccountId.Value;
            action.EntityId = Context.AccountId.Value;
            return action;
        });

        return Ok(result);
    }

    // [HttpGet("/api/v1/Flow({flowId})/[controller]({id})/Form")]
    // [ProducesResponseType(typeof(Form), 200)]
    // public async Task<IActionResult> GetFlowFormAsync(
    //     [FromRoute] Guid flowId,
    //     [FromRoute] Guid id,
    //     [FromQuery] Guid eventTriggerId)
    // {
    //     var result = await _flowService.GetFormAsync(
    //         Context,
    //         flowId: flowId,
    //         actionId: id,
    //         eventTypeId: eventTriggerId
    //     );
    //
    //     return result != null ? Ok(result) : NotFound();
    // }
}