using System;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Services;

namespace Controllers;

[Authorize("lumin-lead")]
[Route("/lumin/v1/[controller]")]
public class AppointmentController : AbstractLeadConversionIntegrationController
{
    public AppointmentController(
        ILogger<AppointmentController> logger,
        IMapper mapper,
        MongoConnection connection,
        AppointmentSchedulerService schedulerService,
        ObjectTypeService objectTypeService,
        ILeadConversionIntegrationService integrationService
        ) : base(logger, mapper, connection, schedulerService, objectTypeService, integrationService)
    {
    }

    [HttpPost]
    public Task<AppointmentResp> CreateAppointment([FromBody] AppointmentReq appt) => CreateAppointmentAsync(appt);

    [HttpGet("/lumin/v1/[controller]({id})")]
    public Task<AppointmentResp> GetAppointment([FromRoute] Guid id) => GetAppointmentAsync(id);

    /// <summary>
    /// Cancel appointment 
    /// </summary>
    [HttpDelete("/lumin/v1/[controller]({id})")]
    public Task<IActionResult> CancelAppointment([FromRoute] Guid id) => CancelAppointmentAsync(id);

    /// <summary>
    /// Cancel appointment and schedule new one
    /// </summary>
    [HttpPut("/lumin/v1/[controller]({id})")]
    public Task<AppointmentResp> RescheduleAppointment([FromRoute] Guid id, [FromBody] AppointmentReq request) => RescheduleAppointmentAsync(id, request);
}