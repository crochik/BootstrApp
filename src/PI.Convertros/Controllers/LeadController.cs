using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Authorize("integration-lead")]
[Route("/convertros/v1/[controller]")]
public class LeadController : AbstractLeadConversionIntegrationController
{
    public LeadController(
        ILogger<LeadController> logger,
        IMapper mapper,
        MongoConnection connection,
        AppointmentSchedulerService schedulerService,
        ObjectTypeService objectTypeService,
        ILeadConversionIntegrationService integrationService
        ) : base(logger, mapper, connection, schedulerService, objectTypeService, integrationService)
    {
    }

    [HttpGet]
    public Task<LeadResp> GetLead() => GetLeadRespAsync();

    [HttpGet("Slots")]
    public Task<IEnumerable<TimeSlot>> GetSlots([FromQuery] DateTime? start, [FromQuery] DateTime? end) => GetSlotsAsync(start, end);

    [HttpPost("Note")]
    [RequestSizeLimit(10_000)]
    public Task<Guid> AddNote([FromBody] AddNoteReq request) => AddNoteAsync(request);

    [HttpPost("Task")]
    [RequestSizeLimit(10_000)]
    public Task<Guid> AddTask([FromBody] AddTaskReq request) => AddTaskAsync(request);
}