using System;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace Controllers;

[Authorize("default")]
[Route("/api/v1/[controller]")]
public class EventTypeController : AbstractNewModelController<IEventTypeAdapter, IEventType, Controllers.Models.EventType>
{
    public EventTypeController(ILogger<EventTypeController> logger, IMapper mapper, IEventTypeAdapter adapter) :
        base(logger, mapper, adapter)
    {
    }

    [HttpGet("/api/v1/Flow({id})/[controller]")]
    [ProducesResponseType(typeof(Controllers.Models.EventType[]), 200)]
    public async Task<IActionResult> GetFlowEventTypesAsync([FromRoute] Guid id, [FromServices] MongoConnection connection)
    {
        var flow = await connection.Filter<Flow>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (flow == null) return NotFound();

        var result = await connection.Filter<EventType>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .In(x => x.EntityId, new Guid?[] { Context.AccountId.Value, null })
            .Eq(x => x.ObjectType, flow.ObjectType)
            // .Regex(x => x.Type, new MongoDB.Bson.BsonRegularExpression($"^{eventType}"))
            .FindAsync();

        return Ok(result);
    }
}