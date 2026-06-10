using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Converters;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Interfaces;

namespace PI.Shared.Services;

[JsonConverter(typeof(StringEnumConverter))]
public enum AppointmentSchedulerError
{
    Validation,
    NotAvailable
}

public class AppointmentSchedulerException : Exception
{
    public AppointmentSchedulerError Error { get; set; }

    public AppointmentSchedulerException(AppointmentSchedulerError error, string message) : base(message)
    {
        Error = error;
    }

    public AppointmentSchedulerException(AppointmentSchedulerError error, string message, Exception innerException) : base(message, innerException)
    {
        Error = error;
    }
}

public abstract class AbstractSchedulerRequest
{
    /// <summary>
    /// Description to be associated with the scheduling event 
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Parent Object id to set the field value
    /// Only Leads are supported right now 
    /// </summary>
    public Guid? ParentObjectId { get; set; }

    /// <summary>
    /// For the future, not supported right now
    /// </summary>
    public string ParentObjectType { get; set; } = nameof(Lead);

    /// <summary>
    /// Field name in the parent object to update
    /// not used yet
    /// </summary>
    public string ParentFieldName { get; set; } = nameof(Lead.NextAppointmentId);
}

/// <summary>
/// Request to cancel appointment (and update field in parent object)
/// </summary>
public class CancelAppointmentRequest : AbstractSchedulerRequest
{
    public Guid AppointmentId { get; set; }
}

/// <summary>
/// Payload to schedule a new appointment (using scheduler)
/// </summary>
public class ScheduleAppointmentRequest : AbstractSchedulerRequest
{
    public DateTime Start { get; set; }

    /// <summary>
    /// Not used as it will use the time from settings
    /// </summary>
    public DateTime? End { get; set; }

    /// <summary>
    /// If provided, it will schedule explicitly for this user
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Seed new appointment properties with initial values
    /// </summary>
    public Dictionary<string, object> InitialValues { get; set; }

    /// <summary>
    /// Automatically cancel previous appointment, if any, for parent object
    /// </summary>
    /// <returns></returns>
    public bool AutoCancelPrevious { get; set; } = true;

    /// <summary>
    /// When re-scheduling an existing appt
    /// </summary>
    public Guid? RescheduleAppointmentId { get; set; }

    public bool SkipAvailabilityCheck { get; set; }
    public bool AllowUnavailableSlots { get; set; }
}

public class AppointmentSchedulerService
{
    // 7-11
    // 11-5
    // 5-8
    // Weekend
    private readonly TimeBlockRule[] TimeBlockRules =
    {
        new TimeBlockRule
        {
            Id = Guid.NewGuid(),
            StartMinutes = 7 * 60,
            EndMinutes = 11 * 60,
            Name = "Morning",
            Days = TimeBlockRule.WeekDays,
        },
        new TimeBlockRule
        {
            Id = Guid.NewGuid(),
            StartMinutes = 17 * 60,
            EndMinutes = 20 * 60,
            Name = "Evening",
            Days = TimeBlockRule.WeekDays,
        },
        new TimeBlockRule
        {
            Id = Guid.NewGuid(),
            StartMinutes = 11 * 60,
            EndMinutes = 17 * 60,
            Name = "Midday",
            Days = TimeBlockRule.WeekDays,
        },
        new TimeBlockRule
        {
            Id = Guid.NewGuid(),
            StartMinutes = 0,
            EndMinutes = 24 * 60,
            Name = "Weekend",
            Days = new[] { DayOfWeek.Saturday, DayOfWeek.Sunday },
        },
    };

    private readonly ILogger<AppointmentSchedulerService> _logger;
    private readonly IMapper _mapper;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly IAppointmentTypeAdapter _appointmentTypeAdapter;
    private readonly IUserAdapter _userAdapter;

    public AppointmentSchedulerService(
        ILogger<AppointmentSchedulerService> logger,
        IMapper mapper,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        IAppointmentTypeAdapter appointmentType,
        IUserAdapter userAdapter
    )
    {
        _logger = logger;
        _mapper = mapper;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _appointmentTypeAdapter = appointmentType;
        _userAdapter = userAdapter;
    }

    private async Task<IEnumerable<CalendarEvent>> GetBusyEventsAsync(Guid accountId, Guid entityId, DateTime startDate, DateTime endDate)
    {
        var events = await _connection.Filter<O365Event>()
            .Eq(x => x.AccountId, accountId)
            .Eq(x => x.EntityId, entityId)
            .Eq(x => x.ShowAs, FreeBusyStatus.Busy)
            .Ne(x => x.Type, CalendarEventType.SeriesMaster) // exclude series master, there should be an occurrence for it
            .Gt(x => x.End, startDate)
            .Lt(x => x.Start, endDate)
            .Eq(x => x.IsCancelled, false)
            .FindAsync();

        return events.Select(x => _mapper.Map<CalendarEvent>(x));
    }

    private async Task<IEnumerable<CalendarEvent>> GetAppointmentsAsync(Guid accountId, Guid entityId, DateTime startDate, DateTime endDate)
    {
        var appts = await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, accountId)
            .Eq(x => x.EntityId, entityId)
            .Eq(x => x.CancelledOn, null)
            .Gt(x => x.End, startDate)
            .Lt(x => x.Start, endDate)
            .FindAsync();

        return appts.Select(x => _mapper.Map<CalendarEvent>(x));
    }

    /// <summary>
    /// Get Existing session for the scheduler (and jwt token)
    /// </summary>
    public async Task<SchedulerSession> GetExistingSession(IEntityContext context, Guid id)
    {
        var session = await  _connection.Filter<SchedulerSession>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();
        
        if (session == null) throw new ForbiddenException(context, "Invalid session");

        // Enforce that the session must be for the same jwt token (ExternalId = jti = Context.Actor.TokenId) 
        if (context.Actor() is AbstractAPIActor actor)
        {
            if (session.ExternalId != actor.TokenId)
            {
                _logger.LogError("Token Mismatch for {SessionId}: {ActorJti} vs {SessionExternalId}", session.Id, actor.TokenId, session.ExternalId);
                // test before enforcing.
                // throw new ForbiddenException(context, "Session Mismatch");
            }
        }

        return session;
    }

    public async Task<bool> CancelFutureAppointmentsAsync(IEntityContext context, Lead lead, Guid? integrationId = null)
    {
        _logger.LogInformation("Cancel appointments for {LeadId}", lead.Id);

        var appointments = await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.LeadId, lead.Id)
            .Eq(x => x.CancelledOn, null)
            .Gte(x => x.Start, DateTime.UtcNow)
            .FindAsync();

        if (appointments.Count < 1)
        {
            _logger.LogInformation("No appointments to cancel for {LeadId}", lead.Id);
            return false;
        }

        var modified = 0;
        foreach (var appt in appointments)
        {
            if (integrationId.HasValue)
            {
                var match = appt.Integrations?.FirstOrDefault(x => x.IntegrationId == integrationId.Value);
                if (match == null) throw new BadRequestException("Can't cancel existing appointment");
            }

            var changed = await CancelAppointmentAndUpdateLeadAsync(context, appt, integrationId);
            if (changed != null) modified++;
        }

        _logger.LogInformation("{Count} appointments canceled for {LeadId}", modified, lead.Id);

        return modified > 0;
    }

    public async Task<(Appointment Appointment, Lead Lead)> GetAppointmentAsync(IEntityContext context, Guid id)
    {
        var appointment = await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (appointment == null) throw NotFoundException.New<Appointment>(id);

        var lead = await GetLeadAsync(context, appointment.LeadId);
        return (appointment, lead);
    }

    /// <summary>
    /// Get default settings for a lead/client
    /// </summary>
    public Task<SchedulerSettings> GetSettingsAsync(IEntityContext context, Lead lead)
        => GetSettingsAsync(context, lead.EntityId);

    public async Task<SchedulerSettings> GetSettingsAsync(IEntityContext context, Guid entityId)
    {
        var settings = await GetSchedulerSettingsAsync(context.AccountId.Value, entityId, context.ClientId);
        if (settings == null) throw new NotFoundException("Scheduler not configured");
        return settings;
    }

    public async Task<SchedulerSettings> GetSchedulerSettingsAsync(Guid accountId, Guid entityId, string clientId)
    {
        var query = _connection.Filter<SchedulerSettings>()
                .Eq(x => x.AccountId, accountId)
                .Eq(x => x.EntityId, entityId)
                .In(x => x.ClientId, new[] { null, clientId })
                .SortDesc(x => x.ClientId) // prefer match to client, null is a fallback
            ;

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get Lead (enforcing access to it)
    /// </summary>
    public async Task<Lead> GetLeadAsync(IEntityContext context, Guid id)
    {
        var objectType = await _objectTypeService.GetAsync(context, nameof(Lead));
        if (!objectType.CanRead(context)) throw new ForbiddenException("Lead");

        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.Id, id)
            .AddConstraints(context, objectType)
            .FirstOrDefaultAsync();

        if (lead == null) throw NotFoundException.New<Lead>(id);

        return lead;
    }

    /// <summary>
    /// Cancel appointment from Appointment Field
    /// </summary>
    public async Task<(Appointment Appointment, Lead Lead)> CancelAppointmentAsync(IEntityContext context, CancelAppointmentRequest request)
    {
        var (appointment, lead) = await GetAppointmentAsync(context, request.AppointmentId);
        return await CancelAppointmentAsync(context, request, appointment, lead);
    }

    /// <summary>
    /// Cancel appointment from Appointment Field
    /// </summary>
    public async Task<(Appointment Appointment, Lead Lead)> CancelAppointmentAsync(IEntityContext context, CancelAppointmentRequest request, Appointment appointment, Lead lead)
    {
        using var scope = _logger.AddScope(new
        {
            request.AppointmentId,
            LeadId = request.ParentObjectId,
            request.ParentObjectType,
            request.ParentFieldName,
        });

        _logger.LogInformation("Cancel Appointment");

        var result = await CancelAppointmentAsync(context, appointment.LeadId, appointment.Id, null);
        if (result == null)
        {
            return (null, null);
        }

        await updateParentObjectAsync();

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            await AddNoteAsync(context, lead, appointment, request.Notes);
        }

        return (result, lead);

        async Task updateParentObjectAsync()
        {
            if (!request.ParentObjectId.HasValue || string.IsNullOrEmpty(request.ParentFieldName) || string.IsNullOrWhiteSpace(request.ParentFieldName))
            {
                // missing info, nothing to do 
                return;
            }

            if (request.ParentObjectType != nameof(Lead))
            {
                // TODO: add support to updating other parent objects
                // ...
                return;
            }

            // "light" validation
            switch (request.ParentObjectType)
            {
                case nameof(Lead):
                    if (request.ParentObjectId.HasValue && lead.Id != request.ParentObjectId) throw new BadRequestException("Parent mismatch");
                    break;

                // will never get here as now...
                case nameof(Appointment):
                    if (request.ParentObjectId.HasValue && appointment.Id != request.ParentObjectId) throw new BadRequestException("Parent mismatch");
                    break;
            }

            _logger.LogInformation("Update Parent object");

            // unset next appointment in lead (if is the cancelled on)
            lead = await _connection.Filter<Lead>()
                .Eq(x => x.AccountId, appointment.AccountId)
                .Eq(x => x.Id, appointment.LeadId)
                .Eq(request.ParentFieldName, appointment.Id)
                .Update
                .Unset(request.ParentFieldName)
                .Set(x => x.LastActor, context.Actor())
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateAndGetOneAsync();

            if (lead != null)
            {
                _logger.LogInformation("{NextAppointmentId} was unset for {LeadId}", appointment.Id, lead.Id);
                //     await _objectTypeService.FireEventAsync(context, lead, EventIds.OnAppointmentCanceled, null, x =>
                //     {
                //         x.Description = "Appointment cancelled";
                //
                //         x.AddRefValue(nameof(Appointment), appointment);
                //         x.AddRefValue(lead);
                //         x.SetMetaValue(nameof(Appointment.LocalDate), appointment.LocalDate);
                //         x.SetMetaValue(nameof(Appointment.LocalTime), appointment.LocalTime);
                //     });
            }
        }
    }

    /// <summary>
    /// Add note for an user initiated change to an appointment/lead  
    /// </summary>
    private async Task<Note> AddNoteAsync(IEntityContext context, Lead lead, Appointment appointment, string content, ContentFormat contentFormat = ContentFormat.PlainText)
    {
        var objectType = await _objectTypeService.GetAsync(context, nameof(Note));

        var note = new Note
        {
            AccountId = context.AccountId.Value,
            EntityId = context.UserId.Value,
            Content = content,
            // ContentFormat = contentFormat,
            ContentType = contentFormat switch
            {
                ContentFormat.Html => "text/html",
                ContentFormat.PlainText => "text/plain",
                ContentFormat.Markdown => "text/markdown",
                _ => null
            },
            Description = content, // TODO: should be the parsed version for other content formats
            Refs = new List<KeyValuePair<string, object>>
            {
                new($"{nameof(Lead)}Id", lead.Id),
                new($"{nameof(Appointment)}Id", appointment.Id),
            },
            FlowId = objectType?.FlowId,
            ObjectStatusId = objectType?.InitialObjectStatusId,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
        };

        return await _objectTypeService.InsertAsync(context, note, e =>
        {
            e.Description ??= $"Note Created";
            e.Action ??= "ObjectCreated";
            e.RefValues ??= new List<KeyValuePair<string, object>>();
            e.RefValues.AddRange(note.Refs);
        });
    }

    /// <summary>
    /// Cancel appointment by id
    /// - will enforce access to lead
    /// - will fire events
    /// - will return null if the appointment can't be cancelled
    /// - will throw exception if the user can't access appt or lead
    /// </summary>
    public async Task<Appointment> CancelAppointmentAsync(IEntityContext context, Guid id, Guid? integrationId = null)
    {
        var (appointment, _) = await GetAppointmentAsync(context, id);

        appointment = await CancelAppointmentAndUpdateLeadAsync(context, appointment, integrationId);

        return appointment;
    }

    /// <summary>
    /// Cancel appointment and fire events for appt and lead
    /// </summary>
    public async Task<Appointment> CancelAppointmentAndUpdateLeadAsync(IEntityContext context, Appointment appointment, Guid? integrationId = null)
    {
        _logger.LogInformation("Cancel {AppointmentId}", appointment.Id);

        var result = await CancelAppointmentAsync(context, appointment.LeadId, appointment.Id, null, integrationId);
        if (result == null)
        {
            // appt status has already changed?
            result = await _connection.Filter<Appointment>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, appointment.Id)
                .FirstOrDefaultAsync();

            return result;
        }

        // unset next appointment in lead (if is the cancelled on)
        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, appointment.AccountId)
            .Eq(x => x.Id, appointment.LeadId)
            .Eq(x => x.NextAppointmentId, appointment.Id)
            .Update
            .Unset(x => x.NextAppointmentId)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        if (lead != null)
        {
            _logger.LogInformation("{NextAppointmentId} was unset for {LeadId}", appointment.Id, lead.Id);
            // await _objectTypeService.FireEventAsync(context, lead, EventIds.OnAppointmentCanceled, null, x =>
            // {
            //     x.Description = "Appointment cancelled";
            //
            //     x.AddRefValue(nameof(Appointment), appointment);
            //     x.AddRefValue(lead);
            //     x.SetMetaValue(nameof(Appointment.LocalDate), appointment.LocalDate);
            //     x.SetMetaValue(nameof(Appointment.LocalTime), appointment.LocalTime);
            //
            //     if (integrationId.HasValue)
            //     {
            //         x.SetRefValue(nameof(Integration), integrationId.Value);
            //         x.SetMetaValue(nameof(Integration), IntegrationIds.GetName(integrationId.Value));
            //     }
            // });
        }

        return result;
    }

    /// <summary>
    /// Cancel appointment (but does not try to update lead)
    /// will only cancel future appointments
    /// </summary>
    private async Task<Appointment> CancelAppointmentAsync(IEntityContext context, Guid leadId, Guid appointmentId, Guid? replacedById, Guid? integrationId = null)
    {
        var now = DateTime.UtcNow;
        var query = _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.LeadId, leadId)
            .Eq(x => x.Id, appointmentId)
            .Eq(x => x.CancelledOn, null)
            .Ne(x => x.IsActive, false)
            .Gte(x => x.Start, now)
            .Update
            .Set(x => x.CancelledOn, now)
            .Set(x => x.IsActive, false)
            .Set(x => x.ReplacedById, replacedById)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, now);

        var modifiedFields = new Dictionary<string, object>
        {
            { nameof(Appointment.CancelledOn), now },
            { nameof(Appointment.IsActive), false },
            { nameof(Appointment.ReplacedById), replacedById },
        };

        if (context.UserId.HasValue)
        {
            query.Set(x => x.CancelledBy, context.UserId);

            modifiedFields.Add(nameof(Appointment.CancelledBy), context.UserId);
        }

        var result = await query.UpdateAndGetOneAsync();

        if (result != null)
        {
            _logger.LogInformation("{AppointmentId} was cancelled for {LeadId}", result.Id, result.LeadId);
            await _objectTypeService.FireObjectUpdatedAsync(context, result, modifiedFields, x =>
            {
                x.Description = "Appointment cancelled";

                x.AddRefValue(nameof(Appointment), result);
                x.SetMetaValue(nameof(Appointment.LocalDate), result.LocalDate);
                x.SetMetaValue(nameof(Appointment.LocalTime), result.LocalTime);

                if (integrationId.HasValue)
                {
                    x.SetRefValue(nameof(Integration), integrationId.Value);
                    x.SetMetaValue(nameof(Integration), IntegrationIds.GetName(integrationId.Value));
                }
            });
        }

        return result;
    }

    /// <summary>
    /// returns "available intervals" for all users
    ///    does not break into slots
    ///    does not dedupe 
    /// </summary>
    private async Task<EntityOpenSlots> GetSlotsForUserAsync(AppointmentType appointmentType, User user, DateTime? start, DateTime? end, SchedulingPolicy? overrideSchedulingPolicy = null)
    {
        var startDate = DateTime.UtcNow;
        if (start.HasValue && start.Value > startDate) startDate = start.Value;
        var endDate = startDate.AddDays(30);
        if (end.HasValue && end.Value < endDate && end.Value > startDate) endDate = end.Value;

        var request = new CalculateAvailabilityRequest(_logger, user, appointmentType.Id, startDate, endDate, overrideSchedulingPolicy);
        request.Result.AppointmentType = appointmentType;
        request.Result.Slots ??= await BuildAvailableSlotsAsync(request);
        request.Result.Events ??= await GetBusyAsync(request.Result.AccountId, request.Result.EntityId, request.Result.Start, request.Result.End);
        request.FilterSlots();

        return request.Result;
    }

    /// <summary>
    /// Get "Unavailable Slots" (e.g. all slots regardless of availability)
    /// </summary>
    public Task<IEnumerable<TimeSlot>> GetUnavailableSlotsAsync(IEntityContext context, SchedulerSession session, DateTime? start, DateTime? end)
        => GetUnavailableSlotsAsync(context, session.Settings, start, end, session.TimeZoneId);

    /// <summary>
    /// Get "Unavailable Slots" (e.g. all slots regardless of availability)
    /// </summary>
    public async Task<IEnumerable<TimeSlot>> GetUnavailableSlotsAsync(IEntityContext context, SchedulerSettings schedulerSettings, DateTime? start, DateTime? end, string timeZoneId)
    {
        var userSettings = schedulerSettings.Settings.FirstOrDefault(x => x.UserId == schedulerSettings.UnavailableSlots?.AssignEntityId);
        if (userSettings == null)
        {
            _logger.LogError("Couldn't find scheduling settings for {UserId}", schedulerSettings.UnavailableSlots?.AssignEntityId);
            throw NotFoundException.New("Missing configuration for user");
        }

        var appointmentType = await _connection.Filter<AppointmentType>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, userSettings.AppointmentTypeId)
            .FirstOrDefaultAsync();

        if (appointmentType == null)
        {
            _logger.LogError("Couldn't find {AppointmentTypeId}", userSettings.AppointmentTypeId);
            throw NotFoundException.New<AppointmentType>(userSettings.AppointmentTypeId);
        }

        var user = await _connection.Filter<User>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, userSettings.UserId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            _logger.LogError("{User} assigned to UnavailableSlots is not active", userSettings.UserId);
            throw NotFoundException.New<User>(userSettings.UserId);
        }

        var settings = new SchedulingSettings
        {
            SchedulingPolicy = schedulerSettings.UnavailableSlots.SchedulingPolicy,
            MinMinutesFromNow = 120,
            StartMinutesMod = 30,
        };

        var availability = schedulerSettings.UnavailableSlots.AvailabilityPolicy switch
        {
            AvailabilityPolicy.UserAvailability => await GetAppointmentTypeAvailabilityAsync(schedulerSettings.UnavailableSlots.AssignEntityId.Value, appointmentType.Id),

            _ => new AppointmentTypeAvailability
            {
                [DayOfWeek.Monday] = businessHours(DayOfWeek.Monday),
                [DayOfWeek.Tuesday] = businessHours(DayOfWeek.Tuesday),
                [DayOfWeek.Wednesday] = businessHours(DayOfWeek.Wednesday),
                [DayOfWeek.Thursday] = businessHours(DayOfWeek.Thursday),
                [DayOfWeek.Friday] = businessHours(DayOfWeek.Friday),
                [DayOfWeek.Saturday] = businessHours(DayOfWeek.Saturday),
            },
        };

        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        var startDate = settings.CalculateMinDate(timeZoneInfo, settings.SchedulingPolicy);
        if (start.HasValue && start.Value > startDate) startDate = start.Value;
        var endDate = startDate.AddDays(30);
        if (end.HasValue && end.Value < endDate && end.Value > startDate) endDate = end.Value;

        var slots = availability.BuildAvailableSlots(startDate, appointmentType.Settings.Duration, startDate, endDate, timeZoneInfo);
        slots = SplitSlots(slots, settings);

        return slots.OrderBy(x => x.Start).ToArray();

        DayAvailability businessHours(DayOfWeek dayOfWeek)
        {
            // TODO: should be by account
            // ...

            return new DayAvailability(dayOfWeek, new[]
            {
                new Slot
                {
                    Id = Guid.NewGuid(),
                    Start = dayOfWeek == DayOfWeek.Saturday ? 9 * 60 : 8 * 60,
                    Duration = dayOfWeek == DayOfWeek.Saturday ? (17 - 9) * 60 : (21 - 8) * 60,
                }
            });
        }
    }

    // /// <summary>
    // /// Get slots for the scheduling session  
    // /// </summary>
    // public async Task<IEnumerable<TimeSlot>> GetSlotsAsync(IEntityContext context, Guid sessionId, DateTime? start, DateTime? end)
    // {
    //     var session = await GetExistingSession(context, sessionId);
    //     return await GetSlotsAsync(context, session, start, end);
    // }

    /// <summary>
    /// Get slots for session (scheduler app) 
    /// </summary>
    public async Task<IEnumerable<TimeSlot>> GetSlotsAsync(IEntityContext context, SchedulerSession session, DateTime? start, DateTime? end)
    {
        var result = await GetEventsAndSlotsAsync(context, session, start, end);
        await _connection.InsertAsync(result);

        var timeSlots = Dedupe(result.Slots);
        return timeSlots;
    }

    /// <summary>
    /// Get slots without session (1st party client) 
    /// </summary>
    public async Task<IEnumerable<TimeSlot>> GetSlotsAsync(IEntityContext context, SchedulerSettings settings, DateTime? start, DateTime? end)
    {
        var result = await GetEventsAndSlotsAsync(context, start, end, settings);
        // await _connection.InsertAsync(result);
        var timeSlots = Dedupe(result.Slots);
        return timeSlots;
    }

    public async Task<EventsAndSlots> GetEventsAndSlotsAsync(IEntityContext context, SchedulerSession session, DateTime? start, DateTime? end)
    {
        var result = await GetEventsAndSlotsAsync(context, start, end, session.Settings);

        result.SessionId = session.Id;

        return result;
    }

    private async Task<EventsAndSlots> GetEventsAndSlotsAsync(IEntityContext context, DateTime? start, DateTime? end, SchedulerSettings settings)
    {
        var appointmentTypeCache = new Dictionary<Guid, AppointmentType>();
        var result = new EventsAndSlots
        {
            Id = Model.NewObjectId(),
            AccountId = settings.AccountId,
            CreatedOn = DateTime.UtcNow,
        };

        foreach (var config in settings.Settings)
        {
            if (!appointmentTypeCache.TryGetValue(config.AppointmentTypeId, out var appointmentType))
            {
                appointmentType = await GetAppointmentTypeAsync(context, config.AppointmentTypeId);
                appointmentTypeCache.Add(appointmentType.Id, appointmentType);
            }

            var user = await _connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, config.UserId)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogInformation("{UserId} not active", config.UserId);
                continue;
            }

            var openSlotsResult = await GetSlotsForUserAsync(appointmentType, user, start, end, settings.OverrideSchedulingPolicy);
            result.Entities[user.Id] = openSlotsResult;

            var slots = SplitSlots(openSlotsResult.Slots, appointmentType.Settings);
            result.Slots.AddRange(slots);
        }

        result.Slots.Sort((a, b) => a.Start.CompareTo(b.Start));

        return result;
    }

    public async Task<IEnumerable<TimeSlot>> GetSlotsAsync(IEntityContext context, User user, Guid appointmentTypeId, DateTime? start, DateTime? end)
    {
        var appointmentType = await GetAppointmentTypeAsync(context, appointmentTypeId);

        var openSlotsResult = await GetSlotsForUserAsync(appointmentType, user, start, end);
        var slots = SplitSlots(openSlotsResult.Slots, appointmentType.Settings);
        return Dedupe(slots);
    }

    private async Task<AppointmentType> GetAppointmentTypeAsync(IEntityContext context, Guid appointmentTypeId)
    {
        var appointmentType = await _connection.Filter<AppointmentType>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, appointmentTypeId)
            .FirstOrDefaultAsync();

        if (appointmentType == null)
        {
            throw new NotFoundException(nameof(AppointmentType), appointmentTypeId);
        }

        return appointmentType;
    }

    private async Task<Entity> GetEntityAsync(IEntityContext context, Guid entityId)
    {
        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, entityId)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            throw new NotFoundException(nameof(Entity), entityId);
        }

        return entity;
    }

    private async Task<SchedulerSession> BuildSessionObjectAsync(IEntityContext context, string referer)
    {
        if (context?.Actor() is not AbstractAPIActor actor || string.IsNullOrWhiteSpace(actor.TokenId))
        {
            throw new BadRequestException("Invalid Context");
        }

        var jti = actor.TokenId;

        var session = await _connection.Filter<SchedulerSession>()
            .Eq(x => x.ExternalId, jti)
            .FirstOrDefaultAsync();

        if (session != null)
        {
            throw new ForbiddenException(context, "Can't reuse session");
        }

        return new SchedulerSession
        {
            Id = Model.NewGuid(),
            AccountId = context.AccountId.Value,
            ExternalId = jti,
            CreatedOn = DateTime.UtcNow,
            LastActor = actor,
            Referer = referer
        };
    }

    public async Task<Appointment> GetNextAppointmentAsync(Lead lead)
    {
        var nextAppt = await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.LeadId, lead.Id)
            .Eq(x => x.CancelledOn, null)
            .Ne(x => x.IsActive, false)
            .Gt(x => x.Start, DateTime.UtcNow)
            .SortAsc(x => x.Start)
            .FirstOrDefaultAsync();

        return nextAppt;
    }

    /// <summary>
    /// Get Existing session for a lead
    /// Will reuse the same session for up to 1 hour
    /// </summary>
    public async Task<SchedulerSession> GetOrCreateSessionForLeadAsync(IEntityContext context, Guid leadId, string referer = null)
    {
        var name = $"{context.ClientId}_{leadId}";

        var session = await _connection.Filter<SchedulerSession>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.EntityId.Value)
            .Eq(x => x.Name, name)
            .Eq(x => x.LeadId, leadId)
            .Eq(x => x.AppointmentId, null)
            .Gt(x => x.CreatedOn, DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)))
            .SortDesc(x => x.CreatedOn)
            .FirstOrDefaultAsync();

        if (session != null)
        {
            _logger.LogInformation("Reusing existing {SessionId} for {LeadId}", session.Id, leadId);
            return session;
        }

        return await CreateSessionForLeadAsync(context, leadId, name, referer, $"{name}_{Guid.NewGuid()}");
    }

    /// <summary>
    /// Create new scheduling session for existing lead (client) 
    /// </summary>
    public async Task<SchedulerSession> CreateSessionForLeadAsync(IEntityContext context, Guid leadId, string name, string referer, string externalId)
    {
        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, leadId)
            .FirstOrDefaultAsync();

        if (lead == null) throw NotFoundException.New<Lead>(leadId);

        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, lead.EntityId)
            .FirstOrDefaultAsync();

        if (entity == null) throw NotFoundException.New<Entity>(lead.EntityId);

        var nextAppt = await GetNextAppointmentAsync(lead);

        // TODO: this is not using the buildSessionObject or loading the entity 
        // so it is not enforcing not reusing token
        // among other things
        // ...
        
        var session = new SchedulerSession
        {
            Id = Model.NewGuid(),
            AccountId = context.AccountId.Value,
            EntityId = context.EntityId.Value,
            Name = name,
            LeadId = leadId,
            AppointmentId = nextAppt?.Id,
            ExternalId = externalId,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            TimeZoneId = entity.TimeZoneId,
            Referer = referer,

            Lead = lead,
            Entity = entity,
            Appointment = nextAppt,
        };

        var query = _connection.Filter<SchedulerSettings>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.EntityId, context.EntityId.Value)
                .In(x => x.ClientId, [null, context.ClientId])
                .SortDesc(x => x.ClientId) // prefer match to client, null is a fallback
            ;

        session.Settings = await query.FirstOrDefaultAsync();
        if (session.Settings == null) throw new Exception("Scheduler configuration not found");

        await _connection.InsertAsync(session);

        return session;
    }

    /// <summary>
    /// Initiate session for an organization / client id
    /// </summary>
    public async Task<SchedulerSession> InitiateSessionForOrganizationAsync(IEntityContext context, Guid entityId, string referer)
    {
        var session = await BuildSessionObjectAsync(context, referer);

        var query = _connection.Filter<SchedulerSettings>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .In(x => x.ClientId, new[] { null, context.ClientId })
                .Eq(x => x.EntityId, entityId)
                .SortDesc(x => x.ClientId) // prefer match to client, null is a fallback
            ;

        session.Settings = await query.FirstOrDefaultAsync();

        if (!session.Settings.IsActive)
        {
            _logger.LogInformation("{SchedulerSettingsId} for {EntityId}: {OutOfServiceMessage} ", session.Settings.Id, entityId, session.Settings.OutOfServiceMessage);
            throw new BadRequestException(session.Settings.OutOfServiceMessage ?? "Not Available");
        }

        return await LoadEntityAsync(context, session);
    }

    /// <summary>
    /// Initiate a session for a launch code 
    /// </summary>
    public async Task<SchedulerSession> InitiateSessionByLaunchCodeAsync(IEntityContext context, string launchCode, string referer)
    {
        var session = await BuildSessionObjectAsync(context, referer);

        var query = _connection.Filter<SchedulerSettings>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .In(x => x.ClientId, new[] { null, context.ClientId })
                .Eq(x => x.ExternalId, launchCode)
                .SortDesc(x => x.ClientId) // prefer match to client, null is a fallback
            ;

        session.Settings = await query.FirstOrDefaultAsync();
        return await LoadEntityAsync(context, session);
    }

    private async Task<SchedulerSession> LoadEntityAsync(IEntityContext context, SchedulerSession session)
    {
        if (session.Settings == null) throw new BadRequestException("Invalid launch code");

        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, session.Settings.EntityId)
            .FirstOrDefaultAsync();

        if (entity == null) throw NotFoundException.New<Entity>(session.Settings.EntityId);

        session.EntityId = session.Settings.EntityId;
        session.Name = session.Settings.Name;
        session.TimeZoneId = entity.TimeZoneId;

        session.Entity = entity;

        await _connection.InsertAsync(session);

        return session;
    }

    // /// <summary>
    // /// Init a scheduling session for an appointment type (optionally for an user?)
    // /// </summary>
    // public async Task<SchedulerSession> InitiateSessionAsync(IEntityContext context, Guid appointmentTypeId, string referer)
    // {
    //     var session = await BuildSessionObjectAsync(context, referer);
    //
    //     var appointmentType = await GetAppointmentTypeAsync(context, appointmentTypeId);
    //     var entity = await GetEntityAsync(context, appointmentType.EntityId);
    //
    //     if (!appointmentType.LeadTypeId.HasValue) throw new BadRequestException("Invalid Appointment Type");
    //
    //     var users = await _userAdapter.GetAvaialbleForAppointmentAsync(appointmentType.Id);
    //     if (users.IsEmpty()) throw new BadRequestException("There isn't any availability");
    //
    //     session.EntityId = entity.Id;
    //     session.Name = entity.Name;
    //
    //     session.Settings = new SchedulerSettings
    //     {
    //         Id = Model.NewGuid(),
    //         AccountId = entity.AccountId,
    //         EntityId = entity.Id,
    //         CreatedOn = DateTime.UtcNow,
    //         LastActor = context.Actor(),
    //         LeadTypeId = appointmentType.LeadTypeId.Value,
    //         Settings = users.OrderBy(x => x.GetEntityRoleId())
    //             .Select((x, i) => new UserSchedulingSettings
    //             {
    //                 UserId = x.Id,
    //                 AppointmentTypeId = appointmentTypeId,
    //                 Priority = i,
    //                 // Constraints = ...
    //             })
    //             .ToArray(),
    //     };
    //
    //     await _connection.InsertAsync(session);
    //
    //     return session;
    // }

    private async Task<IEnumerable<CalendarEvent>> GetBusyAsync(Guid accountId, Guid entityId, DateTime startDate, DateTime endDate)
    {
        var events = await GetBusyEventsAsync(accountId, entityId, startDate, endDate);
        var appts = await GetAppointmentsAsync(accountId, entityId, startDate, endDate);

        return events.Except(appts, new CalendarEventTimeComparer()).Concat(appts);
    }

    public async Task<AppointmentTypeAvailability> GetAppointmentTypeAvailabilityAsync(Guid userId, Guid appointmentTypeId)
    {
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.Id, userId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        var availability = user?.Availability.Where(x => x.AppointmentTypeIds?.Contains(appointmentTypeId) ?? false) ?? Enumerable.Empty<Availability>();

        var dict = new Dictionary<int, List<Slot>>();
        foreach (var a in availability)
        {
            if (dict.TryGetValue((int)a.DayId, out var day))
            {
                day.Add(_mapper.Map<Slot>(a));
            }
            else
            {
                var day2 = new List<Slot>();
                dict[(int)a.DayId] = day2;
                day2.Add(_mapper.Map<Slot>(a));
            }
        }

        var ret = new AppointmentTypeAvailability
        {
            AppointmentTypeId = appointmentTypeId,
        };

        foreach (var slot in dict.Keys)
        {
            ret.Days[slot] = new DayAvailability((DayOfWeek)slot, dict[slot].ToArray());
        }

        return ret;
    }

    /// <summary>
    /// Create slots for availability in range
    /// </summary>
    private async Task<List<TimeSlot>> BuildAvailableSlotsAsync(CalculateAvailabilityRequest request)
    {
        request.Result.Availability = await GetAppointmentTypeAvailabilityAsync(request.Result.EntityId, request.Result.AppointmentTypeId);
        return request.BuildAvailableSlots(request.Result.Availability).ToList();
    }

    /// <summary>
    /// Calculate availability for org
    /// </summary>
    public async Task<IEnumerable<TimeSlotWithCount>> GetOrganizationAvailabilityAsync(IEntityContext context, DateTime start, DateTime end)
    {
        var users = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.OrganizationId, context.OrganizationId.Value)
            .Eq(x => x.IsActive, true)
            .In(x => x.UserRoleId, new[] { nameof(EntityRoleId.Manager), nameof(EntityRoleId.User) })
            .Exists(x => x.Availability[0])
            .FindAsync();

        var dict = new Dictionary<DateTime, Dictionary<int, int>>();
        var mod = 30; // slots every 30 minutes

        foreach (var user in users)
        {
            var availability = await getUserAvailabilityAsync(user);
            if (availability == null) continue;

            foreach (var slot in availability.Result.Slots)
            {
                var slotStart = slot.Start;
                if (!dict.TryGetValue(slotStart.Date, out var day))
                {
                    day = new Dictionary<int, int>();
                    dict.Add(slotStart.Date, day);
                }

                var endMinutes = (slot.End - slot.Start).TotalMinutes;
                // var mod = availability.Result.AppointmentType.Settings.StartMinutesMod ?? 30;
                var startMinutes = slotStart.Hour * 60 + slotStart.Minute;
                for (var c = 0; c <= endMinutes - availability.Result.AppointmentType.Settings.Duration; c += mod)
                {
                    if (!day.TryGetValue(startMinutes + c, out var slotCount))
                    {
                        day[startMinutes + c] = 1;
                    }
                    else
                    {
                        day[startMinutes + c] = slotCount + 1;
                    }
                }
            }
        }

        return dict.SelectMany(day => day.Value.Select(x =>
        {
            var start = day.Key.AddMinutes(x.Key);
            return new TimeSlotWithCount
            {
                Start = start,
                End = start.AddMinutes(mod),
                Tag = x.Value.ToString(),
            };
        }));

        async Task<CalculateAvailabilityRequest> getUserAvailabilityAsync(User user)
        {
            // TODO: this is not right, we should support an user having more than one appointment type availability 
            // ...
            
            var appointmentTypeIds = user.Availability?.SelectMany(x => x.AppointmentTypeIds).Distinct().ToArray();
            if (appointmentTypeIds?.Length != 1)
            {
                return null;
            }

            var apptType = await _appointmentTypeAdapter.GetByIdAsync(appointmentTypeIds[0]);
            if (apptType == null) throw NotFoundException.New<AppointmentType>(appointmentTypeIds[0]);

            var request = new CalculateAvailabilityRequest(_logger, user, apptType.Id, start, end);
            request.Result.AppointmentType = apptType;
            request.Result.Slots = await BuildAvailableSlotsAsync(request);
            request.Result.Events ??= await GetBusyAsync(request.Result.AccountId, request.Result.EntityId, request.Result.Start, request.Result.End);
            request.FilterSlots();

            return request;
        }
    }

    /// <summary>
    /// Calculate Availability Stats 
    /// </summary>
    public async Task<UserAvailability> GetUserAvailabilityAsync(User user, DateTime start, DateTime end)
    {
        var request = await GetOpenSlotsAsync(user, start, end);

        // for now override slots
        var slots = request.Apply(TimeBlockRules);

        // convert to XTimeSlot
        var timeZoneInfo = request.Result.TimeZoneInfo;
        var tSlots = slots.Select(x =>
        {
            if (x is not XTimeSlot tSlot)
            {
                tSlot = new XTimeSlot
                {
                    Start = x.Start,
                    End = x.End,
                };
            }

            var localDate = TimeZoneInfo.ConvertTimeFromUtc(tSlot.Start, timeZoneInfo);
            var localDateOff = new DateTimeOffset(localDate.Year, localDate.Month, localDate.Day, 0, 0, 0, timeZoneInfo.BaseUtcOffset);
            tSlot.LocalDate = localDateOff.ToString("O");
            tSlot.WeekNumber = (int)((tSlot.Start - start).TotalDays / 7);

            return tSlot;
        });

        var availability = new UserAvailability
        {
            AccountId = user.AccountId,
            OrganizationId = user.OrganizationId,
            EntityId = user.Id,
            Name = user.Name,
            Slots = tSlots.ToArray(),
            TimeZoneId = user.TimeZoneId,
            Duration = request.Result.AppointmentType.Settings.Duration,
        };

        availability.CalculateStats();

        return availability;
    }


    /// <summary>
    /// will throw if can't find entity or timzone is not defined
    /// </summary>
    public async Task<CalculateAvailabilityRequest> GetAllSlotsAsync(IEntityContext context, Guid appointmentTypeId, DateTime start, DateTime end)
    {
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, context.UserId.Value)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (user == null) throw NotFoundException.New<User>(context.UserId.Value);

        var request = new CalculateAvailabilityRequest(_logger, user, appointmentTypeId, start, end);
        request.Result.AppointmentType = await _appointmentTypeAdapter.GetByIdAsync(appointmentTypeId);
        request.Result.Slots = await BuildAvailableSlotsAsync(request);

        return request;
    }

    /// <summary>
    /// Only one slot per start time (ordered)
    /// </summary>
    private static IEnumerable<TimeSlot> Dedupe(IEnumerable<TimeSlot> slots)
    {
        return slots.DistinctBy(x => x.Start).OrderBy(x => x.Start).ToArray();
    }

    /// <summary>
    /// Split intervals into slots using settings
    /// </summary>
    private static IEnumerable<TimeSlot> SplitSlots(IEnumerable<TimeSlot> slots, SchedulingSettings settings)
    {
        var duration = TimeSpan.FromMinutes(settings.Duration);
        var mod = settings.StartMinutesMod.GetValueOrDefault(60);
        var off = TimeSpan.FromMinutes(mod);

        foreach (var slot in slots)
        {
            var start = slot.Start;
            if (start.Minute % mod != 0) start += TimeSpan.FromMinutes(mod - start.Minute % mod);
            while (slot.End - start >= duration)
            {
                yield return new TimeSlot
                {
                    Start = start,
                    End = start.Add(duration),
                };

                start += off;
            }
        }
    }

    /// <summary>
    /// returns "available intervals" for all users
    ///    does not break into slots
    ///    does not dedupe 
    /// </summary>
    public async Task<EventsAndSlots> AggregateAsync(AppointmentType appointmentType, DateTime start, DateTime end)
    {
        var users = (await _userAdapter.GetAvaialbleForAppointmentAsync(appointmentType.Id)).ToArray();
        if (users.Length == 0)
        {
            return new EventsAndSlots();
        }

        var result = new EventsAndSlots();
        foreach (var user in users)
        {
            var request = new CalculateAvailabilityRequest(_logger, user, appointmentType.Id, start, end);
            request.Result.AppointmentType = appointmentType;
            request.Result.Slots ??= await BuildAvailableSlotsAsync(request);
            request.Result.Events ??= await GetBusyAsync(request.Result.AccountId, request.Result.EntityId, request.Result.Start, request.Result.End);
            request.FilterSlots();

            result.Entities[user.Id] = request.Result;
            result.Slots.AddRange(request.Result.Slots);

            if (users.Length == 1) return result;
        }

        // sort slots
        result.Slots.Sort((a, b) => a.Start.CompareTo(b.Start));

        return result;
    }

    public async Task<CalculateAvailabilityRequest> GetOpenSlotsAsync(IEntityContext context, DateTime start, DateTime end)
    {
        var user = await _connection.UserQuery(context)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();
        if (user == null) throw NotFoundException.New<User>(context.UserId.Value);
        return await GetOpenSlotsAsync(user, start, end);
    }

    private async Task<CalculateAvailabilityRequest> GetOpenSlotsAsync(User user, DateTime start, DateTime end)
    {
        // TODO: this is not right, we should support an user having more than one appointment type availability 
        // ...
        var appointmentTypeIds = user.Availability?.SelectMany(x => x.AppointmentTypeIds).Distinct().ToArray();
        if (appointmentTypeIds?.Length != 1)
        {
            return null;
        }

        var apptType = await _appointmentTypeAdapter.GetByIdAsync(appointmentTypeIds[0]);
        if (apptType == null) throw NotFoundException.New<AppointmentType>(appointmentTypeIds[0]);

        var request = new CalculateAvailabilityRequest(_logger, user, apptType.Id, start, end);
        request.Result.AppointmentType = apptType;
        request.Result.Slots = await BuildAvailableSlotsAsync(request);
        request.Result.Events ??= await GetBusyAsync(request.Result.AccountId, request.Result.EntityId, request.Result.Start, request.Result.End);
        request.FilterSlots();

        return request;
    }

    public async Task<CalculateAvailabilityRequest> GetOpenSlotsAsync(IEntityContext context, Guid appointmentTypeId, DateTime start, DateTime end)
    {
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, context.UserId.Value)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (user == null) throw NotFoundException.New<User>(context.UserId.Value);

        var request = new CalculateAvailabilityRequest(_logger, user, appointmentTypeId, start, end);
        request.Result.AppointmentType = await _appointmentTypeAdapter.GetByIdAsync(appointmentTypeId);
        request.Result.Slots = await BuildAvailableSlotsAsync(request);
        request.Result.Events ??= await GetBusyAsync(request.Result.AccountId, request.Result.EntityId, request.Result.Start, request.Result.End);
        request.FilterSlots();

        return request;
    }

    public async Task CalculateAsync(CalculateAvailabilityRequest request)
    {
        request.Result.AppointmentType ??= await _appointmentTypeAdapter.GetByIdAsync(request.Result.AppointmentTypeId);
        request.Result.Slots ??= await BuildAvailableSlotsAsync(request);
        request.Result.Events ??= await GetBusyAsync(request.Result.AccountId, request.Result.EntityId, request.Result.Start, request.Result.End);
        request.FilterSlots();
    }

    private async Task<bool> IsAvailableAsync(User user, AppointmentType appointmentType, DateTime start, DateTime end)
    {
        // check availability 
        bool available = IsUserAvailable(user, appointmentType, start, end);

        if (!available)
        {
            _logger.LogInformation("{UserId} is not available for {AppointmentType} for {Start}-{End}", user.Id, appointmentType.Id, start, end);
            return false;
        }

        // any overlap
        var overlap = await GetConflictingO365EventAsync(user, start, end);
        if (overlap != null)
        {
            _logger.LogInformation("{UserId} has a {O365EventId} that overlaps with {Start}-{End}", user.Id, overlap.Id, start, end);
            return false;
        }

        var existing = await GetConflictingAppointmentAsync(user, start, end);
        if (existing != null)
        {
            _logger.LogInformation("{UserId} has a {Appointment} that overlaps with {Start}-{End}", user.Id, existing.Id, start, end);
            return false;
        }

        return true;
    }

    private async Task<Appointment> GetConflictingAppointmentAsync(User user, DateTime start, DateTime end)
    {
        return await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, user.AccountId)
            .Eq(x => x.EntityId, user.Id)
            .Lt(x => x.Start, end)
            .Gt(x => x.End, start)
            .Eq(x => x.IsActive, true)
            .Eq(x => x.CancelledOn, null)
            .FirstOrDefaultAsync();
    }

    private async Task<O365Event> GetConflictingO365EventAsync(User user, DateTime start, DateTime end)
    {
        // any not all-day event overlapping is a show stopper
        var list = await _connection.Filter<O365Event>()
            .Eq(x => x.AccountId, user.AccountId)
            .Eq(x => x.EntityId, user.Id)
            .Eq(x => x.ShowAs, FreeBusyStatus.Busy) // appointmentType.Settings.IncludeStatuses
            .Lt(x => x.Start, end)
            .Gt(x => x.End, start)
            .Eq(x => x.IsCancelled, false)
            .FindAsync();

        foreach (var evt in list)
        {
            if (evt.IsAllDay ?? false)
            {
                // the all day events will have their start and end as UTC dates (no time)
                var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZoneId);
                var firstDayInUtc = TimeZoneInfo.ConvertTimeToUtc(new DateTime(evt.Start.Year, evt.Start.Month, evt.Start.Day), timeZoneInfo);
                var lastDayInUtc = TimeZoneInfo.ConvertTimeToUtc(new DateTime(evt.End.Year, evt.End.Month, evt.End.Day), timeZoneInfo).AddDays(1);
                if (start >= lastDayInUtc || end < firstDayInUtc) continue;
                return evt;
            }

            // any not-full-day event is enough
            return evt;
        }

        return null;
    }

    /// <summary>
    /// Whether the user availability defined for the appointment type includes this slot
    /// </summary>
    private static bool IsUserAvailable(User user, AppointmentType appointmentType, DateTime start, DateTime end)
    {
        if (!user.IsActive) return false;

        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZoneId);
        var localStart = TimeZoneInfo.ConvertTime(start, TimeZoneInfo.Utc, timeZoneInfo);
        var dayId = localStart.DayOfWeek;

        foreach (var availability in user.Availability)
        {
            if (availability.AppointmentTypeIds == null || !availability.AppointmentTypeIds.Contains(appointmentType.Id))
            {
                // not valid for this type
                continue;
            }

            if (availability.DayId != dayId)
            {
                // wrong date
                continue;
            }

            var minutes = availability.StartMinutes % 60;
            var hours = (availability.StartMinutes - minutes) / 60;
            var slotStart = TimeZoneInfo.ConvertTime(new DateTime(localStart.Year, localStart.Month, localStart.Day, hours, minutes, 0), timeZoneInfo, TimeZoneInfo.Utc);
            var slotEnd = slotStart.AddMinutes(availability.EndMinutes - availability.StartMinutes);

            if (start >= slotStart && end <= slotEnd) return true;
        }

        return false;
    }

    public async Task<bool> TestCreateAppointmentAsync(
        IEntityContext context,
        SchedulerSession session,
        EventsAndSlots eventsAndSlots
    )
    {
        foreach (var config in eventsAndSlots.Entities)
        {
            var user = await _connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, config.Key)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();

            var splitSlots = SplitSlots(config.Value.Slots, config.Value.AppointmentType.Settings);
            foreach (var slot in splitSlots)
            {
                if (!IsUserAvailable(user, config.Value.AppointmentType, slot.Start, slot.End))
                {
                    throw new Exception("User was supposed to be available");
                }

                var appt = await GetConflictingAppointmentAsync(user, slot.Start, slot.End);
                if (appt != null)
                {
                    if (config.Value.Events?.Any(x => x.Id == appt.Id.ToString()) ?? false)
                    {
                        throw new Exception("Failed to exclude appt");
                    }

                    // new appointment
                    throw new Exception("ignored appt or new");
                }

                var result = await IsAvailableAsync(user, config.Value.AppointmentType, slot.Start, slot.End);
                if (!result)
                {
                    throw new Exception("User was supposed to be available");
                }
            }
        }

        // var appointmentTypeCache = new Dictionary<Guid, AppointmentType>();

        // foreach (var config in session.Settings.Settings)
        // {
        //     if (!appointmentTypeCache.TryGetValue(config.AppointmentTypeId, out var appointmentType))
        //     {
        //         appointmentType = await GetAppointmentTypeAsync(context, config.AppointmentTypeId);
        //         appointmentTypeCache.Add(appointmentType.Id, appointmentType);
        //     }

        //     if ((start - DateTime.Now).TotalMinutes < appointmentType.Settings.MinMinutesFromNow)
        //     {
        //         // before allowed min
        //         _logger.LogInformation("Not enough heads up to use {appointmentTypeId}: {min} {actual}", appointmentType.Id, appointmentType.Settings.MinMinutesFromNow, (start - DateTime.Now).TotalMinutes);
        //         continue;
        //     }

        //     var user = await _connection.Filter<Entity, User>()
        //         .Eq(x => x.AccountId, context.AccountId.Value)
        //         .Eq(x => x.Id, config.UserId)
        //         .FirstOrDefaultAsync();

        //     if (user == null) throw NotFoundException.New<User>(config.UserId);

        //     // adjust slot to the duration for the user
        //     var endSlot = start.AddMinutes(appointmentType.Settings.Duration);
        //     var isUserAvailable = await IsAvailableAsync(user, appointmentType, start, endSlot);
        //     if (!isUserAvailable) continue;

        //     // update end 
        //     // endSlot;

        //     // appointment = await createAppointmentAsync(appointmentType, user);
        //     return true;
        // }

        return false;
    }

    /// <summary>
    /// Create Appointment for app scheduler (log result)
    /// </summary>
    public async Task<Appointment> CreateAppointmentAsync(IEntityContext context, SchedulerSession session, DateTime start, DateTime end, AppointmentIntegration integration = null, bool allowUnavailable = false, Dictionary<string, object> initialValues = null)
    {
        if (!session.LeadId.HasValue)
        {
            throw new BadRequestException("Missing Lead");
        }

        var request = new ScheduleAppointmentRequest
        {
            ParentFieldName = nameof(Lead.NextAppointmentId),
            ParentObjectType = nameof(Lead),
            ParentObjectId = session.LeadId.Value,
            Start = start,
            End = null, // calculate it
            UserId = null, // calculate it
            AllowUnavailableSlots = allowUnavailable,
            SkipAvailabilityCheck = false,
            InitialValues = initialValues,
        };

        var appointment = await CreateAppointmentBySchedulerAsync(context, request, session.Settings, integration);
        if (appointment == null) throw new AppointmentSchedulerException(AppointmentSchedulerError.NotAvailable, $"No user available for {start}-{end} ({session.Id})");

        await _connection.Filter<SchedulerSession>()
            .Eq(x => x.Id, session.Id)
            .Update
            .Set(x => x.AppointmentId, appointment.Id)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        return appointment;
    }

    /// <summary>
    /// Used for a User to Schedule appointment
    /// </summary>
    public async Task<Result<Appointment>> ScheduleAppointmentByUserAsync(IContextWithActor context, ScheduleAppointmentRequest request, Lead lead)
    {
        var error = await ValidateItCantScheduleAppointmentAsync(context, request, lead);
        if (error != null) return Result.Error<Appointment>(error);

        var settings = await GetSettingsAsync(context, lead);
        var result = await CreateAppointmentByUserAsync(context, request, settings);

        if (result.IsSuccess && !string.IsNullOrWhiteSpace(request.Notes))
        {
            await AddNoteAsync(context, lead, result.Value, request.Notes);
        }

        return result;
    }

    private async Task<string> ValidateItCantScheduleAppointmentAsync(IEntityContext context, ScheduleAppointmentRequest request, Lead lead)
    {
        if (string.IsNullOrWhiteSpace(request.ParentObjectType))
        {
            // enforce permissions checking appointment object type permissions
            var appointmentObjectType = await _objectTypeService.GetAsync(context, nameof(Appointment));
            if (appointmentObjectType == null) return $"{nameof(Appointment)} Not valid for this account";
            if (!appointmentObjectType.RBAC.Can(context, ObjectTypePermission.Create)) return "Forbidden: Create Appointment";

            return null;
        }

        // enforce permissions to field Lead.NextAppointmentId
        var parentObjectType = await _objectTypeService.GetAsync(context, request.ParentObjectType);
        if (parentObjectType == null) return $"{request.ParentObjectType} Not valid for this account";
        if (!parentObjectType.RBAC.Can(context, ObjectTypePermission.Update)) return "Forbidden: Update Object";
        if (!string.IsNullOrWhiteSpace(request.ParentFieldName))
        {
            if (!parentObjectType.Fields.TryGetValue(request.ParentFieldName, out var appointmentField)) return "Invalid Field Name";
            if (!appointmentField.RBAC.CanCreateOnDemand(context)) return "Forbidden: Create Appointment";

            // if (!appointmentField.RBAC.CanUpdate(context))
            // {
            //     // finer grain permission
            //     if (lead.NextAppointmentId.HasValue)
            //     {
            //         if (!appointmentField.RBAC.CanReset(context)) throw new ForbiddenException("Reschedule");
            //     }
            // }
        }

        return null;
    }

    /// <summary>
    /// Create appointment (by user)
    /// </summary>
    private async Task<Result<Appointment>> CreateAppointmentByUserAsync(IEntityContext context, ScheduleAppointmentRequest request, SchedulerSettings settings)
    {
        var buildResult = await BuildAppointmentAsync(context, request, settings);
        if (!buildResult) return buildResult.ConvertTo<Appointment>();

        var builder = buildResult.Value;
        if (builder?.Appointment == null)
        {
            // error
            return Result.Error<Appointment>("Error Building Appointment");
        }

        if (request.InitialValues != null)
        {
            // for now accepts setting ONLY and ANY Refs| field
            // TODO: should allow any field to be set (not just refs?)
            // TODO: should check the object type to make sure it can be set by the user 
            // ...
            // seed initial values
            foreach (var kvp in request.InitialValues)
            {
                if (!kvp.Key.StartsWith("Refs|")) continue;
                builder.Appointment.Refs ??= new Dictionary<string, object>();
                builder.Appointment.Refs[kvp.Key["Refs|".Length..]] = kvp.Value;

                // bad idea but... 
                switch (kvp.Key)
                {
                    case "Refs|sf_WorkOrder":
                    case "Refs|salesforce.WorkOrder": // probably never going to happen but...
                        builder.Appointment.Parent = new ReferencedObject
                        {
                            ObjectType = "salesforce.WorkOrder",
                            ObjectId = kvp.Value,
                        };
                        break;
                }
            }
        }

        // TODO: should also check parentObject, parentFieldName, ....
        // ...
        // switch (request.ParentObjectType)
        // {
        //     case nameof(Lead):
        //         break;
        //     case "salesforce.WorkOrder":
        //     case "sf_WorkOrder":
        //         builder.Appointment.Parent = new ReferencedObject
        //         {
        //             ObjectType = "salesforce.WorkOrder",
        //             ObjectId = request.ParentObjectId // does not work because the reference is not Guid? !?!??!?!
        //         };
        //         break;
        // }

        await AddAppointmentAsync(context, builder);

        if (request.RescheduleAppointmentId.HasValue)
        {
            // rescheduling existing appointment
            // cancel previous and set replacedById 
            _logger.LogInformation("Try to cancel {RescheduleAppointmentId}", request.RescheduleAppointmentId);
            await CancelAppointmentAsync(context, builder.Lead.Id, request.RescheduleAppointmentId.Value, builder.Appointment.Id, builder.Appointment.Integrations?.FirstOrDefault()?.IntegrationId);
        }

        if (request.AutoCancelPrevious && builder.PreviousNextAppointmentId.HasValue && builder.PreviousNextAppointmentId != request.RescheduleAppointmentId)
        {
            // cancel previous appointment if necessary
            _logger.LogInformation("Try to cancel {NextAppointmentId}", builder.PreviousNextAppointmentId);
            await CancelAppointmentAsync(context, builder.Lead.Id, builder.PreviousNextAppointmentId.Value, builder.Appointment.Id, builder.Appointment.Integrations?.FirstOrDefault()?.IntegrationId);
        }

        // TODO: should also check parentObject, parentFieldName, ....
        // ...
        await UpdateNextAppointmentInLeadAsync(context, builder);

        return Result.Success(builder.Appointment);
    }

    /// <summary>
    /// Create Appointment and update lead (for app Scheduler)
    /// </summary>
    private async Task<Appointment> CreateAppointmentBySchedulerAsync(IEntityContext context, ScheduleAppointmentRequest request, SchedulerSettings settings, AppointmentIntegration integration)
    {
        var buildResult = await BuildAppointmentAsync(context, request, settings, integration);
        if (!buildResult.IsSuccess) return null;

        var builder = buildResult.Value;
        if (builder?.Appointment == null)
        {
            // error
            return null;
        }

        builder.Appointment.Tags = (builder.Appointment.Tags ?? Enumerable.Empty<string>())
            .Append("Self Scheduled")
            .ToArray();

        if (request.InitialValues != null)
        {
            // TODO: allow to pass references via initial values for the sched.onl appts?
            // ...

            // foreach (var kvp in request.InitialValues)
            // {
            //     if (!kvp.Key.StartsWith("Refs|")) continue;
            //     builder.Appointment.Refs ??= new Dictionary<string, object>();
            //     builder.Appointment.Refs[kvp.Key["Refs|".Length..]] = kvp.Value;
            // }            
        }

        await AddAppointmentAsync(context, builder);

        if (builder.PreviousNextAppointmentId.HasValue)
        {
            // cancel previous appointment if necessary
            _logger.LogInformation("Try to cancel {NextAppointmentId}", builder.PreviousNextAppointmentId);
            await CancelAppointmentAsync(context, builder.Lead.Id, builder.PreviousNextAppointmentId.Value, builder.Appointment.Id, builder.Appointment.Integrations.FirstOrDefault()?.IntegrationId);
        }

        await UpdateNextAppointmentInLeadAsync(context, builder);

        return builder.Appointment;
    }

    /// <summary>
    /// Convert lead if it hasn't been converted
    /// Update NextAppointmentId 
    /// </summary>
    private async Task UpdateNextAppointmentInLeadAsync(IEntityContext context, AppointmentBuilder builder)
    {
        // mark lead as converted if it hasn't yet
        var updatedLead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, builder.Lead.Id)
            .Eq(x => x.ConvertedOn, null)
            .Update
            .Set(x => x.ConvertedOn, DateTime.UtcNow)
            .Set(x => x.NextAppointmentId, builder.Appointment.Id)
            .Set(x => x.AssignedEntityId, builder.Appointment.EntityId)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        var modifiedFields = default(Dictionary<string, object>);
        if (updatedLead == null)
        {
            // make sure appointment is set
            updatedLead = await _connection.Filter<Lead>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, builder.Lead.Id)
                .Ne(x => x.NextAppointmentId, builder.Appointment.Id)
                .Update
                .Set(x => x.NextAppointmentId, builder.Appointment.Id)
                .Set(x => x.AssignedEntityId, builder.Appointment.EntityId)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
                .UpdateAndGetOneAsync();

            if (updatedLead == null)
            {
                _logger.LogInformation("Nothing to update on lead");
                return;
            }

            modifiedFields = new Dictionary<string, object>
            {
                { nameof(Lead.NextAppointmentId), updatedLead.NextAppointmentId },
                { nameof(Lead.AssignedEntityId), updatedLead.AssignedEntityId },
            };
        }
        else
        {
            modifiedFields = new Dictionary<string, object>
            {
                { nameof(Lead.ConvertedOn), updatedLead.ConvertedOn },
                { nameof(Lead.NextAppointmentId), updatedLead.NextAppointmentId },
                { nameof(Lead.AssignedEntityId), updatedLead.AssignedEntityId },
            };
        }

        builder.Lead = updatedLead;

        _logger.LogInformation("This was the first appointment for {LeadId}: {NextAppointmentId}", updatedLead.Id, updatedLead.NextAppointmentId);

        await _objectTypeService.FireObjectUpdatedAsync(builder.User.Context, updatedLead, modifiedFields, evt =>
        {
            evt.Description = "Lead Converted";

            evt.AddRefValue(nameof(Appointment), builder.Appointment.Id);
            evt.SetMetaValue(nameof(User), builder.User.Name);
            if (updatedLead != null) evt.SetMetaValue(nameof(Lead), updatedLead.Name);
            if (builder.Organization != null) evt.SetMetaValue(nameof(Organization), builder.Organization.Name);
        });
    }

    /// <summary>
    /// Add appointment to database and fire create event
    /// </summary>
    /// <param name="context"></param>
    /// <param name="builder"></param>
    private async Task AddAppointmentAsync(IEntityContext context, AppointmentBuilder builder)
    {
        var entityIds = context.GetEntityIds().ToArray();
        if (entityIds.Length < 1)
        {
            _logger.LogError("Invalid Context {Role}", context.Role);
            throw new Exception("Failed to add appointment to database");
        }

        builder.Appointment.EntityIds = entityIds;
        builder.Appointment.LastModifiedOn = DateTime.UtcNow;
        builder.Appointment.LastActor = context.Actor();

        await _connection.InsertAsync(builder.Appointment);

        // fire both create object events
        await _objectTypeService.FireCreateEventAsync(builder.User.Context, builder.Appointment, evt =>
        {
            evt.Description = "Appointment Scheduled";
            evt.Action ??= "ObjectCreated";

            evt.AddRefValue(nameof(Lead), builder.Appointment.LeadId);
            evt.AddRefValue(nameof(AppointmentType), builder.Appointment.AppointmentTypeId);

            evt.SetMetaValue(nameof(User), builder.User.Name);
            if (builder.Lead != null) evt.SetMetaValue(nameof(Lead), builder.Lead.Name);
            if (builder.Organization != null) evt.SetMetaValue(nameof(Organization), builder.Organization.Name);
            evt.SetMetaValue(nameof(AppointmentType), builder.AppointmentType.Name);
            evt.SetMetaValue(nameof(Appointment.LocalDate), builder.Appointment.LocalDate);
            evt.SetMetaValue(nameof(Appointment.LocalTime), builder.Appointment.LocalTime);
            evt.SetMetaValue("TimeZone", builder.User.TimeZoneId);

            var integration = builder.Appointment.Integrations?.FirstOrDefault();
            if (integration != null)
            {
                evt.SetRefValue(nameof(Integration), integration.IntegrationId);
                evt.SetMetaValue(nameof(Integration), IntegrationIds.GetName(integration.IntegrationId));
            }
        });
    }

    /// <summary>
    /// "builder" to create appointment object
    /// shared between app scheduler and api scheduler (by user) 
    /// </summary>
    private async Task<Result<AppointmentBuilder>> BuildAppointmentAsync(
        IEntityContext context,
        ScheduleAppointmentRequest request,
        SchedulerSettings settings,
        AppointmentIntegration integration = null)
    {
        if (request.ParentObjectType != nameof(Lead)) throw new BadRequestException("Only Lead is supported as the Parent Object");

        if (request.Start < DateTime.UtcNow) return Result.Error<AppointmentBuilder>("Can't schedule appointment in the past");

        var leadId = request.ParentObjectId.Value;
        var userId = request.UserId;

        var appointmentTypeCache = new Dictionary<Guid, AppointmentType>();

        var filteredSettings = settings.Settings;
        if (userId.HasValue)
        {
            filteredSettings = filteredSettings
                .Where(x => x.UserId == userId.Value)
                .ToArray();

            if (filteredSettings.Length < 1) return Result.Error<AppointmentBuilder>("User not enabled for scheduling");
        }

        if (filteredSettings.Length < 1) return Result.Error<AppointmentBuilder>("No User enabled for scheduling");

        var priorities = filteredSettings
            .GroupBy(x => x.Priority)
            .OrderBy(x => x.Key);

        foreach (var priority in priorities)
        {
            var list = priority.ToList();
            while (!list.IsEmpty())
            {
                var index = Random.Shared.Next(list.Count);
                var config = list[index];
                list.RemoveAt(index);

                var builder = await addAppointmentAsync(config, !request.SkipAvailabilityCheck);
                if (builder != null)
                {
                    _logger.LogInformation("{UserId} can take appt", config.UserId);
                    return Result.Success(builder);
                }
            }
        }

        if (userId.HasValue)
        {
            _logger.LogError("User is not available");
            return Result.Error<AppointmentBuilder>("User is not available");
        }

        // not found
        var allowUnavailable = request.AllowUnavailableSlots && (settings?.UnavailableSlots?.AssignEntityId.HasValue ?? false);
        if (!allowUnavailable)
        {
            _logger.LogError("No user available and not configured for unavailable slots: {AllowUnavailable} {AssignEntityId}", allowUnavailable, settings.UnavailableSlots?.AssignEntityId);
            return Result.Error<AppointmentBuilder>("No user available for this slot");
        }

        // allow
        var userSettings = filteredSettings.FirstOrDefault(x => x.UserId == settings.UnavailableSlots.AssignEntityId.Value);
        if (userSettings == null)
        {
            _logger.LogError("Didn't find Scheduling Settings for {UserId}", settings.UnavailableSlots.AssignEntityId.Value);
            return Result.Error<AppointmentBuilder>("Missing Configuration for placeholder User");
        }

        var unavailableBuilder = await addAppointmentAsync(userSettings, false);
        if (unavailableBuilder == null)
        {
            return Result.Error<AppointmentBuilder>("Placeholder user not available.");
        }

        return Result.Success(unavailableBuilder);

        async Task<AppointmentBuilder> addAppointmentAsync(UserSchedulingSettings config, bool checkAvailability)
        {
            var (appointmentType, user) = await getAppointmentTypeAndUserAsync(config, checkAvailability);
            if (user == null)
            {
                _logger.LogInformation("{UserId} is not available at {Start} {CheckAvailability}", config.UserId, request.Start, checkAvailability);
                return null;
            }

            // adjust slot to the duration for the user
            var endSlot = request.End ?? request.Start.AddMinutes(appointmentType.Settings.Duration);
            if (!checkAvailability)
            {
                return await successAsync(user, appointmentType, endSlot, settings.UnavailableSlots);
            }

            var isUserAvailable = await IsAvailableAsync(user, appointmentType, request.Start, endSlot);
            if (!isUserAvailable)
            {
                _logger.LogInformation("{UserId} is not available at {Start}-{End}", config.UserId, request.Start, endSlot);
                return null;
            }

            return await successAsync(user, appointmentType, endSlot);
        }

        async Task<(AppointmentType AppointmentType, User User)> getAppointmentTypeAndUserAsync(UserSchedulingSettings config, bool checkAvailability)
        {
            if (!appointmentTypeCache.TryGetValue(config.AppointmentTypeId, out var appointmentType))
            {
                appointmentType = await GetAppointmentTypeAsync(context, config.AppointmentTypeId);
                appointmentTypeCache.Add(appointmentType.Id, appointmentType);
            }

            if (checkAvailability && (request.Start - DateTime.Now).TotalMinutes < appointmentType.Settings.MinMinutesFromNow)
            {
                // before allowed min
                _logger.LogInformation("Not enough heads up to use {AppointmentTypeId}/{UserId}: {Min} {Actual}", appointmentType.Id, config.UserId, appointmentType.Settings.MinMinutesFromNow, (request.Start - DateTime.Now).TotalMinutes);
                return (null, null);
            }

            var user = await _connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, config.UserId)
                .FirstOrDefaultAsync();

            return (appointmentType, user);
        }

        async Task<AppointmentBuilder> successAsync(User user, AppointmentType appointmentType, DateTime endSlot, UnavailableSlots unavailableSlots = null)
        {
            var lead = await _connection.Filter<Lead>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, leadId)
                // .IncludeField(x => x.Name)
                // .IncludeField(x => x.NextAppointmentId)
                // .IncludeField(x => x.AccountId)
                // .IncludeField(x => x.EntityId)
                .FirstOrDefaultAsync();

            var organization = await _connection.Filter<Entity, Organization>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, user.OrganizationId)
                // .IncludeField("_t")
                // .IncludeField(x => x.Name)
                .FirstOrDefaultAsync();

            var unavailableFlow = unavailableSlots?.AssignEntityId != null &&
                                  user.Id != context.UserId &&
                                  context.Role != EntityRoleId.Manager;

            var flowId = unavailableFlow ? unavailableSlots.FlowId ?? appointmentType.InitialFlowId : appointmentType.InitialFlowId;
            var objectStatusId = unavailableFlow ? unavailableSlots.ObjectStatusId ?? appointmentType.InitialObjectStatusId : appointmentType.InitialObjectStatusId;
            if (!flowId.HasValue)
            {
                var objectType = await _objectTypeService.GetObjectTypeAsync<Appointment>(context);
                flowId = objectType.InitialFlowId;
                objectStatusId = objectType.InitialObjectStatusId;
            }

            var duration = (int)(endSlot - request.Start).TotalMinutes;
            var subject = duration != appointmentType.Settings.Duration && request.End.HasValue ? $"{duration}min Appointment" : appointmentType.Name;
            subject = $"{lead.Name} - {appointmentType.Description ?? subject}";

            var appointment = new Appointment
            {
                Id = Guid.NewGuid(),
                LeadId = leadId,
                Parent = new ReferencedObject
                {
                    ObjectType = nameof(Lead),
                    ObjectId = leadId,
                },
                AccountId = context.AccountId.Value,
                EntityId = user.Id,
                OrganizationId = organization?.Id,
                CreatedBy = context.UserId,
                CreatorId = context.UserId,
                CreatedOn = DateTime.UtcNow,
                LastActor = context.Actor(),
                AppointmentTypeId = appointmentType.Id,
                IsAllDay = false,
                Name = subject,
                Start = request.Start,
                End = endSlot,
                FlowId = flowId,
                ObjectStatusId = objectStatusId,
                Tool = context.ClientId,
                Tags = getTags().ToArray(),
                // Data = extra,
                // Notes = notes,
                // public string WebLink { get; set; }
                // public DateTime CreatedOn { get; set; }
                // public DateTime? ExpiresOn { get; set; }
            };

            if (integration != null)
            {
                appointment.Integrations = [integration];
            }

            appointment.CalculateMetaData(user.TimeZoneId);

            return new AppointmentBuilder
            {
                Settings = settings,
                PreviousNextAppointmentId = lead.NextAppointmentId,
                AppointmentType = appointmentType,
                User = user,
                End = endSlot,
                Lead = lead,
                Organization = organization,
                Appointment = appointment,
            };

            IEnumerable<string> getTags()
            {
                if (context.UserId.HasValue && context.UserId.Value == user.Id) yield return "User";
                else if (context.Role == EntityRoleId.Manager) yield return "Organization";

                if (unavailableFlow) yield return "Unavailable Slot";
                if (!userId.HasValue) yield return "Auto User";
            }
        }
    }

    // /// <summary>
    // /// Create Appointment and update lead (for app Scheduler)
    // /// </summary>
    // private async Task<Appointment> CreateAppointmentAsync(IEntityContext context, SchedulerSettings settings, Guid leadId, DateTime start, DateTime? end = null, AppointmentIntegration integration = null, Guid? userId = null)
    // {
    //     var appointment = default(Appointment);
    //     var appointmentTypeCache = new Dictionary<Guid, AppointmentType>();
    //
    //     var usersSettings = settings.Settings;
    //     if (userId.HasValue)
    //     {
    //         usersSettings = usersSettings
    //             .Where(x => x.UserId == userId.Value).ToArray();
    //     }
    //
    //     foreach (var config in usersSettings)
    //     {
    //         if (!appointmentTypeCache.TryGetValue(config.AppointmentTypeId, out var appointmentType))
    //         {
    //             appointmentType = await GetAppointmentTypeAsync(context, config.AppointmentTypeId);
    //             appointmentTypeCache.Add(appointmentType.Id, appointmentType);
    //         }
    //
    //         if ((start - DateTime.Now).TotalMinutes < appointmentType.Settings.MinMinutesFromNow)
    //         {
    //             // before allowed min
    //             _logger.LogInformation("Not enough heads up to use {AppointmentTypeId}/{UserId}: {Min} {Actual}", appointmentType.Id, config.UserId, appointmentType.Settings.MinMinutesFromNow, (start - DateTime.Now).TotalMinutes);
    //             continue;
    //         }
    //
    //         var user = await _connection.Filter<Entity, User>()
    //             .Eq(x => x.AccountId, context.AccountId.Value)
    //             .Eq(x => x.Id, config.UserId)
    //             .FirstOrDefaultAsync();
    //
    //         if (user == null)
    //         {
    //             _logger.LogError("{UserId} does not exist", config.UserId);
    //             throw NotFoundException.New<User>(config.UserId);
    //         }
    //
    //         // adjust slot to the duration for the user
    //         var endSlot = start.AddMinutes(appointmentType.Settings.Duration);
    //         var isUserAvailable = await IsAvailableAsync(user, appointmentType, start, endSlot);
    //         if (!isUserAvailable)
    //         {
    //             _logger.LogInformation("{UserId} is not available at {Start}-{End}", config.UserId, start, endSlot);
    //             continue;
    //         }
    //
    //         // update end 
    //         end = endSlot;
    //
    //         appointment = await createAppointmentAsync(appointmentType, user);
    //         break;
    //     }
    //
    //     return appointment;
    //
    //     async Task<Appointment> createAppointmentAsync(AppointmentType appointmentType, User user)
    //     {
    //         var partialLead = await _connection.Filter<Lead>()
    //             .Eq(x => x.AccountId, context.AccountId)
    //             .Eq(x => x.Id, leadId)
    //             .IncludeField(x => x.Name)
    //             .IncludeField(x => x.NextAppointmentId)
    //             .IncludeField(x => x.AccountId)
    //             .IncludeField(x => x.EntityId)
    //             .FirstOrDefaultAsync();
    //
    //         var organization = await _connection.Filter<Entity, Organization>()
    //             .Eq(x => x.AccountId, context.AccountId)
    //             .Eq(x => x.Id, user.OrganizationId)
    //             .IncludeField("_t")
    //             .IncludeField(x => x.Name)
    //             .FirstOrDefaultAsync();
    //
    //         var flowId = appointmentType.InitialFlowId;
    //         var objectStatusId = appointmentType.InitialObjectStatusId;
    //         if (!flowId.HasValue)
    //         {
    //             var objectType = await _objectTypeService.GetObjectTypeAsync<Appointment>(context);
    //             flowId = objectType.InitialFlowId;
    //             objectStatusId = objectType.InitialObjectStatusId;
    //         }
    //
    //         var subject = $"{partialLead.Name} - {appointmentType.Description ?? appointmentType.Name}";
    //
    //         var appointment = new Appointment
    //         {
    //             Id = Guid.NewGuid(),
    //             LeadId = leadId,
    //             AccountId = context.AccountId.Value,
    //             EntityId = user.Id,
    //             CreatedBy = context.UserId,
    //             CreatedOn = DateTime.UtcNow,
    //             LastActor = context.Actor(),
    //             AppointmentTypeId = appointmentType.Id,
    //             IsAllDay = false,
    //             Subject = subject,
    //             Start = start,
    //             End = end.Value,
    //             FlowId = flowId,
    //             ObjectStatusId = objectStatusId,
    //             Tool = context.ClientId,
    //             // Data = extra,
    //             // CreatedBy = scheduledBy,
    //             // Notes = notes,
    //             // Tool = tool,
    //             // public string WebLink { get; set; }
    //             // public DateTime CreatedOn { get; set; }
    //             // public DateTime? ExpiresOn { get; set; }
    //         };
    //
    //         if (integration != null)
    //         {
    //             appointment.Integrations = new[] { integration };
    //         }
    //
    //         appointment.UpdateLocalStrings(user.TimeZoneId);
    //         appointment = await _appointmentAdapter.AddAsync(user.Context, appointment);
    //
    //         // fire both create object events
    //         await _objectTypeService.FireCreateEventAsync(user.Context, appointment, evt =>
    //         {
    //             evt.Description = "Appointment Scheduled";
    //             evt.Action ??= "ObjectCreated";
    //
    //             evt.AddRefValue(nameof(Lead), appointment.LeadId);
    //             evt.AddRefValue(nameof(AppointmentType), appointment.AppointmentTypeId);
    //
    //             evt.SetMetaValue(nameof(User), user.Name);
    //             if (partialLead != null) evt.SetMetaValue(nameof(Lead), partialLead.Name);
    //             if (organization != null) evt.SetMetaValue(nameof(Organization), organization.Name);
    //             evt.SetMetaValue(nameof(AppointmentType), appointmentType.Name);
    //             evt.SetMetaValue(nameof(Appointment.LocalDate), appointment.LocalDate);
    //             evt.SetMetaValue(nameof(Appointment.LocalTime), appointment.LocalTime);
    //             evt.SetMetaValue("TimeZone", user.TimeZoneId);
    //
    //             if (integration != null)
    //             {
    //                 evt.SetRefValue(nameof(Integration), integration.IntegrationId);
    //                 evt.SetMetaValue(nameof(Integration), IntegrationIds.GetName(integration.IntegrationId));
    //             }
    //         });
    //
    //         if (partialLead.NextAppointmentId.HasValue)
    //         {
    //             // cancel previous appointment if necessary
    //             _logger.LogInformation("Try to cancel {NextAppointmentId}", partialLead.NextAppointmentId);
    //             await CancelAppointmentAsync(context, partialLead.Id, partialLead.NextAppointmentId.Value, integration?.IntegrationId);
    //         }
    //
    //         // mark lead as converted if it hasn't yet
    //         var updatedLead = await _connection.Filter<Lead>()
    //             .Eq(x => x.AccountId, context.AccountId.Value)
    //             .Eq(x => x.Id, partialLead.Id)
    //             .Eq(x => x.ConvertedOn, null)
    //             .Update
    //             .Set(x => x.ConvertedOn, DateTime.UtcNow)
    //             .Set(x => x.LastModifiedOn, DateTime.UtcNow)
    //             .Set(x => x.LastActor, context.Actor())
    //             .Set(x => x.NextAppointmentId, appointment.Id)
    //             .Set(x => x.AssignedEntityId, appointment.EntityId)
    //             .UpdateAndGetOneAsync();
    //
    //         if (updatedLead == null)
    //         {
    //             // make sure appointment is set
    //             updatedLead = await _connection.Filter<Lead>()
    //                 .Eq(x => x.AccountId, context.AccountId.Value)
    //                 .Eq(x => x.Id, partialLead.Id)
    //                 .Ne(x => x.NextAppointmentId, appointment.Id)
    //                 .Update
    //                 .Set(x => x.NextAppointmentId, appointment.Id)
    //                 .Set(x => x.AssignedEntityId, appointment.EntityId)
    //                 .Set(x => x.LastModifiedOn, DateTime.UtcNow)
    //                 .Set(x => x.LastActor, context.Actor())
    //                 .UpdateAndGetOneAsync();
    //
    //             if (updatedLead == null)
    //             {
    //                 _logger.LogInformation("Nothing to update on lead");
    //                 return appointment;
    //             }
    //         }
    //
    //         _logger.LogInformation("This was the first appointment for {LeadId}: {NextAppointmentId}", updatedLead.Id, updatedLead.NextAppointmentId);
    //
    //         await _objectTypeService.FireObjectUpdatedAsync(user.Context, updatedLead, evt =>
    //         {
    //             evt.Description = $"Lead Converted";
    //             evt.Action ??= "ObjectUpdated";
    //
    //             evt.AddRefValue(nameof(Appointment), appointment.Id);
    //             evt.SetMetaValue(nameof(User), user.Name);
    //             if (updatedLead != null) evt.SetMetaValue(nameof(Lead), updatedLead.Name);
    //             if (organization != null) evt.SetMetaValue(nameof(Organization), organization.Name);
    //         });
    //
    //         return appointment;
    //     }
    // }

    // private async Task<User> LoadBalanceUserAsync(AppointmentType apptType, DateTime start, DateTime end)
    // {
    //     var users = await GetAvailableUsersForApptAsync(apptType, start, end);
    //
    //     switch (users.Length)
    //     {
    //         case 0: return null;
    //         case 1: return users[0];
    //     }
    //
    //     // TODO: implement different configurable algorithms
    //     // ...
    //
    //     var regUsers = users.Where(u => u.UserRoleId.Equals(EntityRoleId.User.ToString())).ToArray();
    //     if (regUsers.Length > 0)
    //     {
    //         int index = _rnd.Next(regUsers.Length);
    //         return regUsers[index];
    //     }
    //     else
    //     {
    //         int index = _rnd.Next(users.Length);
    //         return users[index];
    //     }
    // }

    // private async Task<User[]> GetAvailableUsersForApptAsync(AppointmentType appointmentType, DateTime start, DateTime end)
    // {
    //     // get users for appt
    //     var users = await _userAdapter.GetAvaialbleForAppointmentAsync(appointmentType.Id);
    //     var list = new List<User>();
    //     foreach (var user in users)
    //     {
    //         var isAvail = await IsAvailableAsync(user, appointmentType, start, end);
    //         if (isAvail)
    //         {
    //             list.Add(user);
    //         }
    //     }
    //
    //     return list.ToArray();
    // }

    // public async Task<bool> EnableAsync(Guid entityId)
    // {
    //     var user = await _userAdapter.GetByIdAsync(entityId);
    //
    //     // for now assume if it has a leadtype it has already been initialized
    //     var leadTypes = await _leadTypeAdapter.GetForEntityAsync(user.Context);
    //     var leadType = leadTypes.FirstOrDefault(x => x.EntityId == user.OrganizationId.Value);
    //     if (leadType != null) return true;
    //
    //     // get bare minimun necessary to work
    //     // add leadtype and appttype
    //     // add to org if user belongs to one
    //
    //     Guid id;
    //     string name;
    //     if (user.OrganizationId.HasValue)
    //     {
    //         id = user.OrganizationId.Value;
    //         name = "Organization";
    //     }
    //     else
    //     {
    //         id = user.Id;
    //         name = "User";
    //     }
    //
    //     var addedLeadType = await _leadTypeAdapter.CreateAsync(new LeadType
    //     {
    //         Id = id,
    //         EntityId = id,
    //         Name = name,
    //         // Settings
    //     });
    //
    //     var apptType = await _appointmentTypeAdapter.CreateAsync(new AppointmentType
    //     {
    //         Id = id,
    //         LeadTypeId = addedLeadType.Id, // will match if for now :)
    //         EntityId = id,
    //         Name = name
    //     });
    //
    //     return true;
    // }

    private async Task<Appointment> GetNextUpcomingAsync(Guid leadId, Guid appointmentTypeId, DateTime start)
        => await _connection.Filter<Appointment>()
            .Eq(x => x.LeadId, leadId)
            .Eq(x => x.AppointmentTypeId, appointmentTypeId)
            .Eq(x => x.CancelledOn, null)
            .Gte(x => x.Start, start)
            .SortAsc(x => x.CreatedBy)
            .FirstOrDefaultAsync();

    private async Task<Appointment> GetLastForLeadAsync(Guid leadId, Guid appointmentTypeId)
        => await _connection.Filter<Appointment>()
            .Eq(x => x.LeadId, leadId)
            .Eq(x => x.AppointmentTypeId, appointmentTypeId)
            .SortDesc(x => x.CreatedBy)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<User>> GetPossibleUsersAsync(IEntityContext context, Guid leadId, Guid appointmentTypeId)
    {
        var appointmentType = await _appointmentTypeAdapter.GetByIdAsync(appointmentTypeId);
        if (appointmentType == null) throw new AppointmentSchedulerException(AppointmentSchedulerError.NotAvailable, "Appointment Type not found");

        var org = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, context.OrganizationId.Value)
            .FirstOrDefaultAsync();

        if (org == null || !appointmentType.EntityId.Equals(org.Id))
        {
            // TODO: should it allow the appointmenttype to be owned by the user calling this?
            // ...
            throw new AppointmentSchedulerException(AppointmentSchedulerError.Validation, "Invalid Appointment Type");
        }

        Guid? apptUserId = null;
        switch (appointmentType.Settings.UserPolicy)
        {
            case UserPolicy.SameOfNextAppt:
                var next = await GetNextUpcomingAsync(leadId: leadId, appointmentTypeId: appointmentTypeId, start: DateTime.UtcNow);
                if (next != null) apptUserId = next.EntityId;
                break;

            case UserPolicy.SameOfLastAppt:
                var lastAppt = await GetLastForLeadAsync(leadId: leadId, appointmentTypeId: appointmentTypeId);
                if (lastAppt != null) apptUserId = lastAppt.EntityId;
                break;

            case UserPolicy.AnyInOrganization:
            default:
                break;
        }

        var users = await _userAdapter.GetAsync(org.Context);
        if (context.Role == EntityRoleId.Manager)
        {
            // manager
            if (apptUserId.HasValue)
            {
                // limit to same user (if is part of org)
                return users.Where(user => user.Id.Equals(apptUserId));
            }

            // any user in the org
            return users;
        }

        // not manager
        if (apptUserId.HasValue)
        {
            if (context.UserId.Value.Equals(apptUserId))
            {
                // last appt assigned to this user
                return new[] { users.First(x => x.Id.Equals(apptUserId)) };
            }

            // not allowed
            return Array.Empty<User>();
        }

        // limit to this user (if is part of org)
        return users.Where(user => context.UserId.Value.Equals(user.Id));
    }

    public async Task<DataViewResponse> GetUserAvailabilityAsync(IEntityContext context, DataViewRequest request)
    {
        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddDays(7);

        var result = default(IEnumerable<object>);
        if (request.Criteria.TryGetUidValueFromEqCondition("UserId", out var userId))
        {
            var user = await _connection.UserQuery(context.AccountId.Value, userId).FirstOrDefaultAsync();
            var availability = await GetUserAvailabilityAsync(user, startDate, endDate);
            result = availability?.Stats.Select(x => new
            {
                _id = x.Key,
                x.Value.Name,
                x.Value.Count,
                x.Value.FirstDate,
            });
        }

        var response = new DataViewResponse
        {
            Request = request,
            View = new DataView
            {
                Name = "User Availability",
                DefaultSort = nameof(TimeBlockStats.FirstDate),
                KeyField = "_id",
                Fields = new FormField[]
                {
                    new HiddenField
                    {
                        Name = "_id",
                    },
                    new TextField
                    {
                        Name = "name",
                        Label = "Type",
                    },
                    new NumberField
                    {
                        Name = "count",
                        Label = "Count",
                    },
                    new DateTimeField
                    {
                        Name = "firstDate",
                        Label = "First Availability"
                    }
                },
                FilterForm = new Form.Models.Form
                {
                    Fields = new FormField[]
                    {
                        new ReferenceField
                        {
                            Name = "UserId",
                            Label = "Pick User",
                            ReferenceFieldOptions =
                            {
                                ObjectType = "User",
                            },
                            IsRequired = true,
                        }
                    }
                }
            },
            Result = result ?? Enumerable.Empty<object>(),
        };

        return response.UpdateFields();
    }

    public async Task<DataViewResponse> GetOrgAvailabilityAsync(IEntityContext context, DataViewRequest request)
    {
        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddDays(7);

        var list = new List<object>();
        if (request.Criteria.TryGetUidValueFromEqCondition("OrganizationId", out var orgId))
        {
            var users = await _connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.OrganizationId, orgId)
                .Eq(x => x.IsActive, true)
                .Exists(x => x.Availability[0])
                .FindAsync();

            var userStats = new List<UserAvailability>();

            var org = new UserAvailability
            {
                Id = Guid.Empty,
                EntityId = Guid.Empty,
                Name = "Organization",
            };

            userStats.Add(org);

            foreach (var user in users)
            {
                var availability = await GetUserAvailabilityAsync(user, startDate, endDate);
                if (availability == null) continue;
                userStats.Add(availability);

                foreach (var a in availability.Stats)
                {
                    if (!org.Stats.TryGetValue(a.Key, out var bucket))
                    {
                        bucket = new TimeBlockStats
                        {
                            Name = a.Key,
                        };

                        org.Stats.Add(a.Key, bucket);
                    }

                    bucket.Count += a.Value.Count;
                    if (!bucket.FirstDate.HasValue || bucket.FirstDate > a.Value.FirstDate) bucket.FirstDate = a.Value.FirstDate;
                }
            }

            foreach (var availability in userStats)
            {
                list.AddRange(availability?.Stats.Select(x => new
                {
                    _id = $"{availability.EntityId}-{x.Key}",
                    availability.Id,
                    User = availability.Name,
                    x.Value.Name,
                    x.Value.Count,
                    x.Value.FirstDate,
                }));
            }
        }

        var response = new DataViewResponse
        {
            Request = request,
            View = new DataView
            {
                Name = "Organization Availability",
                DefaultSort = "_id",
                KeyField = "_id",
                Fields = new FormField[]
                {
                    new HiddenField
                    {
                        Name = "_id",
                    },
                    new TextField
                    {
                        Name = "user",
                        Label = "User",
                    },
                    new TextField
                    {
                        Name = "name",
                        Label = "Type",
                    },
                    new NumberField
                    {
                        Name = "count",
                        Label = "Count",
                    },
                    new DateTimeField
                    {
                        Name = "firstDate",
                        Label = "First Availability"
                    }
                },
                FilterForm = new Form.Models.Form
                {
                    Fields = new FormField[]
                    {
                        new ReferenceField
                        {
                            Name = "OrganizationId",
                            Label = "Pick Organization",
                            ReferenceFieldOptions =
                            {
                                ObjectType = "Organization",
                            },
                            IsRequired = true,
                        }
                    }
                }
            },
            Result = list?.ToArray() ?? Enumerable.Empty<object>(),
        };

        return response.UpdateFields();
    }

    // TODO: move it into the service
    // ???
    public IEnumerable<NamedSlot> CalculateSuggestedSlots(string timeZoneId, IEnumerable<TimeSlot> slots)
    {
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);

        var asap = false;
        var morning = false;
        var afternoon = false;

        // do not offer these ...
        var earlyMorning = false;
        var evening = false;
        var saturday = false;
        var sunday = false;

        foreach (var slot in slots)
        {
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(slot.Start, timezone);
            if (!asap)
            {
                yield return GetNamedSlot(localNow, localTime, slot, "ASAP: ");
                asap = true;
            }

            if (!earlyMorning && localTime.Hour < 9)
            {
                yield return GetNamedSlot(localNow, localTime, slot, "Early morning, ");
                earlyMorning = true;
            }

            if (!morning && localTime.Hour < 12)
            {
                yield return GetNamedSlot(localNow, localTime, slot, "Morning, ");
                morning = true;
            }

            if (!afternoon && localTime.Hour >= 12 && localTime.Hour < 19)
            {
                yield return GetNamedSlot(localNow, localTime, slot, "Afternoon, ");
                afternoon = true;
            }

            if (!evening && localTime.Hour > 18)
            {
                yield return GetNamedSlot(localNow, localTime, slot, "Evening, ");
                evening = true;
            }

            if (!saturday && localTime.DayOfWeek == DayOfWeek.Saturday)
            {
                yield return GetNamedSlot(localNow, localTime, slot);
                saturday = true;
            }

            if (!sunday && localTime.DayOfWeek == DayOfWeek.Sunday)
            {
                yield return GetNamedSlot(localNow, localTime, slot);
                sunday = true;
            }

            if (asap && earlyMorning && morning && afternoon && evening && saturday && sunday)
            {
                yield break;
            }
        }
    }

    public static NamedSlot GetNamedSlot(DateTime localNow, DateTime localTime, TimeSlot slot, string prefix = "")
    {
        var name = "";
        var diff = localTime.Date - localNow.Date;
        if (diff.Days == 0)
        {
            name = "Today";
        }
        else if (diff.Days < 2)
        {
            name = "Tomorrow";
        }
        else if (diff.Days < 7)
        {
            name = localTime.DayOfWeek.ToString();
        }
        else if (diff.Days < 14)
        {
            name = "Next " + localTime.DayOfWeek;
        }
        else
        {
            name = localTime.DayOfWeek.ToString();
        }

        return new NamedSlot
        {
            Name = prefix + name,
            Start = slot.Start,
            End = slot.End
        };
    }
}

public class AppointmentBuilder
{
    public Guid? PreviousNextAppointmentId { get; set; }
    public SchedulerSettings Settings { get; set; }
    public AppointmentType AppointmentType { get; set; }
    public DateTime End { get; set; }
    public User User { get; set; }
    public Lead Lead { get; set; }
    public Organization Organization { get; set; }
    public Appointment Appointment { get; set; }
}