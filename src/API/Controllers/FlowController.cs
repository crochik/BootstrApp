using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Controllers.Models;
using Crochik.Mongo;
using FlowActions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;
using Services;
using EventType = PI.Shared.Models.EventType;
using Flow = Controllers.Models.Flow;
using FlowStep = Controllers.Models.FlowStep;

namespace Controllers;

[Authorize("default")]
[Route("/api/v1/[controller]")]
public class FlowController : AbstractNewModelController<IFlowAdapter, IFlow, Flow>
{
    private readonly FlowService _flowService;
    // private readonly ObjectTypeService _objectTypeService;
    // private readonly IEventTypeAdapter _eventTypeAdapter;

    public FlowController(
        ILogger<FlowController> logger,
        IMapper mapper,
        FlowService flowService,
        // ObjectTypeService objectTypeService,
        // IEventTypeAdapter eventTypeAdapter,
        IFlowAdapter adapter
    ) : base(logger, mapper, adapter)
    {
        _flowService = flowService;
        // _objectTypeService = objectTypeService;
        // _eventTypeAdapter = eventTypeAdapter;
    }

    [HttpGet("/api/v1/[controller]({id})/Steps")]
    public async Task<IEnumerable<FlowStep>> GetEventsAsync([FromRoute] Guid id)
    {
        var flow = await _adapter.GetByIdAsync(id);
        if (flow == null) throw new NotFoundException(nameof(PI.Shared.Models.Flow), id);

        var events = flow.GetSteps() ?? Enumerable.Empty<PI.Shared.Models.FlowStep>();
        return events.Select(e => _mapper.Map<FlowStep>(e));
    }

    // private async Task<ParsedOptions> ParseAsync(Guid id, FlowStepUpdate step)
    // {
    //     var flow = await _adapter.GetByIdAsync(id);
    //     if (flow == null) throw new NotFoundException(nameof(PI.Shared.Models.Flow), id);
    //     if (!CanUpdate(Context, flow)) throw new ForbiddenException(Context, "Can't update");
    //
    //     var eventType = await _eventTypeAdapter.GetByIdAsync(step.EventIdTrigger);
    //     if (eventType == null) throw new NotFoundException(nameof(EventType), step.EventIdTrigger);
    //
    //     var context = new ParseContext
    //     {
    //         ActionId = step.ActionId,
    //         CurrentStatusId = step.CurrentStatusId,
    //         Description = step.Description,
    //         EntityId = flow.EntityId,
    //         EventIdTrigger = step.EventIdTrigger,
    //         IconName = step.IconName,
    //         ObjectType = eventType.ObjectType ?? flow.ObjectType,
    //         Options = step.Options,
    //         EntityContext = Context,
    //     };
    //
    //     var parseResult = await _flowService.ParseAsync(context);
    //     if (!parseResult.Success) throw new BadRequestException(parseResult.ErrorMessage);
    //
    //     return parseResult;
    // }

    // [HttpPost("/api/v1/[controller]({id})/Step")]
    // public async Task<FlowStep> AddEventAsync([FromRoute] Guid id, [FromBody] FlowStepUpdate step)
    // {
    //     var parseResult = await ParseAsync(id, step);
    //     var added = await _adapter.AddAsync(id, parseResult.Step);
    //     return _mapper.Map<FlowStep>(added);
    // }
    //
    // [HttpPut("/api/v1/[controller]({id})/Step({stepId})")]
    // public async Task<FlowStep> UpdateStepAsync([FromRoute] Guid id, [FromRoute] Guid stepId, [FromBody] FlowStepUpdate step)
    // {
    //     var parseResult = await ParseAsync(id, step);
    //     var updated = await _adapter.UpdateStepAsync(id, stepId, parseResult.Step);
    //     return _mapper.Map<FlowStep>(updated);
    // }

    [HttpDelete("/api/v1/[controller]({id})/Step({stepId})")]
    [ProducesResponseType(typeof(FlowStep), 200)]
    public async Task<IActionResult> DeleteStepAsync([FromRoute] Guid id, [FromRoute] Guid stepId)
    {
        var flow = await _adapter.GetByIdAsync(id);
        if (flow == null) return NotFound();
        if (!CanUpdate(Context, flow)) return Forbid();

        var step = await _adapter.DeleteStepsAsync(flow.Id, stepId);
        if (!step) return NotFound("Step");

        return Ok(step);
    }

    // [Authorize("default")]
    // [HttpPost("/api/v1/[controller]({id})/{objectType}/Lookup")]
    // public async Task<IEnumerable<ReferenceValue>> LookupAsync([FromRoute] Guid id, [FromRoute] string objectType, DataViewRequest request, [FromServices] MongoConnection connection)
    // {
    //     var flow = await _adapter.GetByIdAsync(id);
    //     if (flow == null) throw new NotFoundException(nameof(PI.Shared.Models.Flow), id);
    //
    //     // objectType == "Trigger"
    //     var eventIds = _flowService.GetOutputEventIds(flow).ToArray();
    //     if (eventIds.Length == 0)
    //     {
    //         return Enumerable.Empty<ReferenceValue>();
    //     }
    //
    //     var eventTypes = await connection.Filter<EventType>()
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .OrBuilder(
    //             q => q.In(x => x.Id, eventIds),
    //             q => q.OfType<EventType, Trigger, SystemTrigger>(x => x.Trigger)
    //                 .OrBuilder(
    //                     q => q.Exists(x => x.ObjectType, false),
    //                     q => q.Eq(x => x.ObjectType, flow.ObjectType)
    //                 ),
    //             q => q.OfType<EventType, Trigger, UserTrigger>(x => x.Trigger)
    //                 .OrBuilder(
    //                     q => q.Exists(x => x.ObjectType, false),
    //                     q => q.Eq(x => x.ObjectType, flow.ObjectType)
    //                 )
    //         )
    //         .SortAsc(x => x.Name)
    //         .FindAsync();
    //
    //     return eventTypes.Select(x => new ReferenceValue
    //     {
    //         Id = x.Id.ToString(),
    //         Value = x.Name
    //     });
    // }
}