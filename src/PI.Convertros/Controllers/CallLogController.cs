using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Models;
using Services;

namespace Controllers;

[Route("/convertros/v1/[controller]")]
public class CallLogController : APIController
{
    public string ConvertrosProvider = "Convertros";

    private static Guid ObjectTypeId = Guid.Parse("82bf6df4-d919-468d-85ab-acd6c8992dc0");

    private readonly ILogger _logger;
    private readonly MongoConnection _connection;
    private readonly ConvertrosService _service;

    public CallLogController(ILogger<CallLogController> logger, MongoConnection connection, ConvertrosService service)
    {
        _logger = logger;
        _connection = connection;
        _service = service;
    }

    [HttpPost]
    [Authorize("integration")]
    // [RequestSizeLimit(10_000)]
    public async Task<IActionResult> BulkAddCallLogAsync([FromBody] CallLog[] request)
    {
        await UpsertAsync(request);
        await _service.UpsertCommunicationNotesAsync(Context, request);
        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("{objectTypeId}")]
    // [RequestSizeLimit(10_000)]
    public async Task<IActionResult> BulkAddAsync([FromBody] CallLog[] request, [FromRoute] Guid objectTypeId)
    {
        if (objectTypeId != ObjectTypeId) return Forbid();

        await UpsertAsync(request);
        await _service.UpsertCommunicationNotesAsync(Context, request);

        return Ok();
    }

    [HttpPut("CommunicationNote")]
    [Authorize("admin")]
    public async Task<IActionResult> ImportNotes()
    {
        var _ = Task.Run(async () => await _service.ImportNotesAsync(Context));
        return Ok();
    }

    private async Task UpsertAsync(CallLog[] request)
    {
        _logger.LogInformation("Received request with {Records}", request?.Length);

        var now = DateTime.UtcNow;
        var list = new List<UpdateOneModel<CallLog>>();
        foreach (var call in request)
        {
            var update = _connection.Filter<CallLog>()
                .Eq(x => x.Id, call.Id)
                .Update
                .SetOnInsert(x => x.Id, call.Id)
                .SetOnInsert(x => x.Date, call.Date)
                .SetOnInsert(x => x.PhoneNumber, Lead.GetNormalizedPhoneNumber(call.PhoneNumber))
                .SetOnInsert(x => x.Direction, call.Direction)
                .SetOnInsert(x => x.LeadId, call.LeadId)
                .SetOnInsert(x => x.CreatedOn, now)
                .Set(x => x.DispositionCode, call.DispositionCode)
                .Set(x => x.DispositionName, call.DispositionName)
                .Set(x => x.LastModifiedOn, now)
                ;

            if (!string.IsNullOrEmpty(call.Link))
            {
                update.Set(x => x.Link, call.Link);
            }

            list.Add(update.UpdateOneModel(true));
        }

        await _connection.BulkWriteAsync(list);
    }
}

[BsonCollection("convertros.CallLog")]
public class CallLog
{
    [BsonId] public string Id { get; set; }

    public DateTime Date { get; set; }
    public string PhoneNumber { get; set; }
    public string Direction { get; set; }
    public string DispositionCode { get; set; }
    public string DispositionName { get; set; }

    [JsonProperty("sourceInternalId")] public Guid LeadId { get; set; }

    public DateTime CreatedOn { get; set; }
    public DateTime LastModifiedOn { get; set; }
    
    public String Link { get; set; }
}