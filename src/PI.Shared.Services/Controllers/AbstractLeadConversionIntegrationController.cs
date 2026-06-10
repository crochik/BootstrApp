using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.Shared.Controllers;

public class AbstractLeadConversionIntegrationController : APIController
{
    protected Guid LeadId
    {
        get
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == "pi_lead_id");
            if (claim == null) throw new ForbiddenException();
            return Guid.Parse(claim.Value);
        }
    }

    protected readonly ILogger<AbstractLeadConversionIntegrationController> _logger;
    protected readonly IMapper _mapper;
    protected readonly MongoConnection _connection;
    protected readonly AppointmentSchedulerService _schedulerService;
    protected readonly ObjectTypeService _objectTypeService;
    protected readonly ILeadConversionIntegrationService _integrationService;

    public AbstractLeadConversionIntegrationController(
        ILogger<AbstractLeadConversionIntegrationController> logger,
        IMapper mapper,
        MongoConnection connection,
        AppointmentSchedulerService schedulerService,
        ObjectTypeService objectTypeService,
        ILeadConversionIntegrationService integrationService
    )
    {
        _logger = logger;
        _mapper = mapper;
        _connection = connection;
        _schedulerService = schedulerService;
        _objectTypeService = objectTypeService;
        _integrationService = integrationService;
    }

    /// <summary>
    /// API: get slots for lead 
    /// </summary>
    protected async Task<IEnumerable<TimeSlot>> GetSlotsAsync(DateTime? start, DateTime? end)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId
        });

        _logger.LogInformation("Get Slots");

        var session = await _schedulerService.GetOrCreateSessionForLeadAsync(Context, LeadId);
        return await _schedulerService.GetSlotsAsync(Context, session, start, end);
    }

    /// <summary>
    /// API: Get lead with appt 
    /// </summary>
    protected async Task<LeadResp> GetLeadRespAsync()
    {
        using var scope = _logger.AddScope(new
        {
            LeadId
        });

        var lead = await GetLeadAsync();

        var result = _mapper.Map<LeadResp>(lead);

        var appointment = await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.LeadId, lead.Id)
            .Eq(x => x.CancelledOn, null)
            .Gt(x => x.Start, DateTime.UtcNow)
            .SortAsc(x => x.Start)
            .FirstOrDefaultAsync();

        if (appointment != null)
        {
            result.IsConverted = true;
            result.NextAppointment = _mapper.Map<Appointment>(appointment);
        }

        _logger.LogInformation("Get Lead Info: {IsActive} {IsConverted} {NextApptStart} {NextApptTool}", result.IsActive, result.IsConverted, result.NextAppointment?.Start, appointment?.Tool);

        return result;
    }

    /// <summary>
    /// API: add note to lead
    /// </summary>
    protected async Task<Guid> AddNoteAsync(AddNoteReq request)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId,
            request.Subject,
            request.Format,
        });

        _logger.LogInformation("Add Note");

        var result = await _integrationService.AddNoteAsync(Context, LeadId, request.Subject, request.Content, request.Format);

        _logger.LogInformation("{NoteId} added", result);
        return result;
    }

    /// <summary>
    /// API: add tasks to lead 
    /// </summary>
    protected async Task<Guid> AddTaskAsync(AddTaskReq request)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId,
            request.ExternalId,
            request.Type,
            request.Subject,
            request.Format,
        });

        _logger.LogInformation("Add Task");

        // TODO: resolve users (by id, by name, by email, by role, by external integration id, ...)
        // ...

        // TODO: resolve type
        // ...

        // TODO: add new task object
        // ...

        var result = await _integrationService.AddNoteAsync(Context, LeadId, request.Subject, request.Content, request.Format);

        _logger.LogInformation("{NoteId} added", result);
        return result;
    }

    /// <summary>
    /// API: Create Appointment
    /// </summary>
    protected async Task<AppointmentResp> CreateAppointmentAsync(AppointmentReq appt)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId,
            appt?.Start,
            appt?.End,
        });

        _logger.LogInformation("Add Appointment");

        return await ScheduleAppointmentAsync(appt);
    }

    /// <summary>
    /// API: Get Appointment
    /// </summary>
    protected async Task<AppointmentResp> GetAppointmentAsync(Guid id)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId,
            AppointmentId = id
        });

        _logger.LogInformation("Get Appointment");

        var lead = await GetLeadAsync();
        var appointment = await GetActiveAppointmentInTheFutureAsync(lead, id);
        return _mapper.Map<AppointmentResp>(appointment);
    }

    /// <summary>
    /// API: Cancel appointment 
    /// </summary>
    protected async Task<IActionResult> CancelAppointmentAsync(Guid id)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId,
            AppointmentId = id
        });

        _logger.LogInformation("Cancel Appointment");

        var lead = await GetLeadAsync();
        await CancelAppointmentAsync(lead, id);

        return Ok();
    }

    /// <summary>
    /// API: Cancel appointment and schedule new one
    /// </summary>
    protected async Task<AppointmentResp> RescheduleAppointmentAsync(Guid id, AppointmentReq request)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId,
            AppointmentId = id,
            request.Start,
            request.End
        });

        _logger.LogInformation("Reschedule Appointment");

        var appointment = await ScheduleAppointmentAsync(request);

        _logger.LogInformation("Replaced {AppointmentId} with {NewAppointmentId}", id, appointment.Id);

        return appointment;
    }

    private async Task<Lead> GetLeadAsync()
    {
        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, LeadId)
            .FirstOrDefaultAsync();

        if (lead == null) throw new NotFoundException("Lead", LeadId);

        return lead;
    }

    protected async Task<AppointmentResp> ScheduleAppointmentAsync(AppointmentReq request)
    {
        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.Id, LeadId)
            .FirstOrDefaultAsync();

        if (lead == null || !lead.IsActive)
        {
            throw new BadRequestException("Lead not found or inactive");
        }
        
        var session = await _schedulerService.GetOrCreateSessionForLeadAsync(Context, LeadId);
        DateTime start = request.Start.Value;
        DateTime end = request.End.Value;

        try
        {
            var integration = new AppointmentIntegration
            {
                IntegrationId = _integrationService.IntegrationId,
                ExternalId = Guid.NewGuid().ToString(),
                Status = "Scheduled",
            };

            var result = await _schedulerService.CreateAppointmentAsync(Context, session, start, end, integration);

            if (result != null)
            {
                await UpdateStatusAsync("Converted", nameof(IExternalLeadIntegration.Converted));
            }

            return _mapper.Map<AppointmentResp>(result);
        }
        catch (AppointmentSchedulerException ex) when (ex.Error == AppointmentSchedulerError.NotAvailable)
        {
            await AddErrorAsync(session, start, end, ex);
            throw new ConflictException(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{SessionId}: Failed to book appointment for {LeadId}, {Start}-{End}", session.Id, LeadId, start, end);
            await AddErrorAsync(session, start, end, ex);
            throw;
        }

        async Task AddErrorAsync(SchedulerSession session, DateTime start, DateTime end, Exception ex)
        {
            await _connection.Filter<SchedulerSession>()
                .Eq(x => x.Id, session.Id)
                .Update
                .Push(x => x.Errors, new SchedulingError
                {
                    Slot = new TimeSlot
                    {
                        Start = start,
                        End = end,
                    },
                    Error = ex.Message,
                })
                .UpdateOneAsync();
        }
    }

    protected async Task<Appointment> CancelAppointmentAsync(Lead lead, Guid id)
    {
        var appointment = await GetActiveAppointmentInTheFutureAsync(lead, id);
        return await CancelAppointmentAsync(lead, appointment);
    }

    protected async Task<Appointment> CancelAppointmentAsync(Lead lead, Appointment appointment)
    {
        appointment = await _schedulerService.CancelAppointmentAndUpdateLeadAsync(Context, appointment, _integrationService.IntegrationId);
        if (appointment == null) throw new Exception("State mismatch");

        return appointment;
    }

    protected async Task<Appointment> GetActiveAppointmentInTheFutureAsync(Lead lead, Guid id)
    {
        var appointment = await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, id)
            .Eq(x => x.LeadId, lead.Id)
            .Eq(x => x.CancelledOn, null)
            .Gt(x => x.Start, DateTime.UtcNow)
            .FirstOrDefaultAsync();

        if (appointment == null) throw new NotFoundException("Appointment", id);

        return appointment;
    }

    protected async Task UpdateStatusAsync(string status, string milestone)
    {
        var now = DateTime.UtcNow;
        var result = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, LeadId)
            .ElemMatchBuilder(
                x => x.Integrations,
                q => q
                    .Eq(x => x.IntegrationId, _integrationService.IntegrationId)
                    .Eq(milestone, default(DateTime?))
            )
            .Update
            .Set($"{nameof(Lead.Integrations)}.$.{milestone}", now)
            .Set($"{nameof(Lead.Integrations)}.$.{nameof(IExternalLeadIntegration.Status)}", status)
            .Set(x => x.LastModifiedOn, now)
            .Set(x => x.LastActor, Context.Actor)
            .UpdateAndGetOneAsync();

        if (result != null)
        {
            _logger.LogInformation("Changed status to {Status}", status);

            var name = IntegrationIds.GetName(_integrationService.IntegrationId);
            await _objectTypeService.FireObjectUpdatedAsync(
                Context,
                result,
                new Dictionary<string, object>
                {
                    { $"{nameof(Lead.Integrations)}|{_integrationService.IntegrationId}|{milestone}", now },
                    { $"{nameof(Lead.Integrations)}|{_integrationService.IntegrationId}|{nameof(IExternalLeadIntegration.Status)}", status },
                },
                evt =>
                {
                    evt.Description = $"Lead Integration status for {name} changed to {status}";
                    evt.SetRefValue(nameof(Integration), _integrationService.IntegrationId);
                    evt.SetMetaValue(nameof(Integration), name);
                    evt.SetMetaValue(milestone, true);
                }
            );
        }
    }

    public class AppointmentReq
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }


    public class AppointmentResp
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Description { get; set; }
        public Guid EntityId { get; set; }

        public Guid LeadId { get; set; }

        // public string Subject { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string LocalDate { get; set; }
        public string LocalTime { get; set; }
        public string TimeZoneId { get; set; }
    }

    public class AppointmentProfile : Profile
    {
        public AppointmentProfile()
        {
            CreateMap<Appointment, AppointmentResp>(MemberList.Destination)
                ;
        }
    }

    public class AddNoteReq
    {
        public string Subject { get; set; }
        public string Content { get; set; }
        public ContentFormat Format { get; set; }

        public string ExternalId { get; set; }

        public string[] Users { get; set; }
    }

    public class AddTaskReq : AddNoteReq
    {
        public string Type { get; set; }
    }

    public class LeadResp
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public string Description { get; set; }

        public Guid EntityId { get; set; }

        public bool IsActive { get; set; }

        /// <summary>
        /// New property to track lead communication preferences (using updates from integrations)
        /// </summary>
        public Dictionary<string, string> CommunicationPreferences { get; set; }

        public bool IsConverted { get; set; }

        public Appointment NextAppointment { get; set; }
    }

    public class LeadProfile : Profile
    {
        public LeadProfile()
        {
            CreateMap<Lead, LeadResp>(MemberList.Destination)
                .ForMember(d => d.IsConverted, o => o.MapFrom(s => s.ConvertedOn.HasValue))
                .ForMember(d => d.NextAppointment, o => o.Ignore()) // calculated
                ;
        }
    }
}