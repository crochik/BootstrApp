using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Controllers.Models;
using Crochik.Extensions;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Layout;
using PI.Shared.Requests;
using PI.Shared.Services;
using Appointment = PI.Shared.Models.Appointment;
using Lead = PI.Shared.Models.Lead;
using Organization = Controllers.Models.Organization;

namespace Controllers;

/// <summary> 
/// Used by the Appointment Field / scheduler page
/// </summary>
[Route("/api/v1/Scheduler")]
[Authorize("default")]
public class AppointmentSchedulerController(
    IMapper mapper,
    ILogger<AppointmentSchedulerController> logger,
    MongoConnection connection,
    AppointmentSchedulerService schedulerService)
    : APIController
{
    [Authorize("default")]
    [HttpGet("/api/v1/Scheduler/User({userId})/Slots")]
    public async Task<SlotsResp> GetSlotsForUserAsync([FromRoute] Guid userId, Guid? appointmentTypeId, DateTime? start, DateTime? end)
    {
        using var scope = logger.AddScope(new
        {
            UserId = userId,
            AppointmentTypeId = appointmentTypeId,
        });

        logger.LogInformation("Get Slots for Lead/client/user");

        if (!appointmentTypeId.HasValue) throw new BadRequestException("AppointmentTypeId required");

        var user = await connection.Filter<Entity, PI.Shared.Models.User>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, userId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (user == null) throw NotFoundException.New<PI.Shared.Models.User>(userId);

        var slots = (await schedulerService.GetSlotsAsync(Context, user, appointmentTypeId.Value, start, end)).ToArray();

        var result = new SlotsResp
        {
            SuggestedSlots = schedulerService.CalculateSuggestedSlots(user.TimeZoneId, slots),
            Slots = slots,
            TimeZoneId = user.TimeZoneId,
        };

        return result;
    }

    [Authorize("default")]
    [HttpGet("/api/v1/Scheduler/Entity({id})/Slots")]
    public async Task<SlotsResp> GetSlotsForEntityAsync([FromRoute] Guid id, DateTime? start, DateTime? end)
    {
        using var scope = logger.AddScope(new
        {
            EntityId = id,
        });

        logger.LogInformation("Get Slots for Entity/client");

        var settings = await schedulerService.GetSettingsAsync(Context, id);
        var slots = (await schedulerService.GetSlotsAsync(Context, settings, start, end)).ToArray();

        var entity = await connection.Filter<Entity>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        var timeZoneId = entity.TimeZoneId;

        var result = new SlotsResp
        {
            SuggestedSlots = schedulerService.CalculateSuggestedSlots(timeZoneId, slots),
            Slots = slots,
            TimeZoneId = timeZoneId,
            Users = await GetUserSettingsForOrganizationAsync(id, settings),
            // AssignedEntityId = lead.AssignedEntityId,
        };

        return result;
    }

    [Authorize("default")]
    [HttpGet("/api/v1/Scheduler/Lead({id})/Settings")]
    public async Task<SchedulerSettings> GetSettingsAsync([FromRoute] Guid id)
    {
        var lead = await schedulerService.GetLeadAsync(Context, id);
        var settings = await schedulerService.GetSettingsAsync(Context, lead);

        // only profile users can use UnavailableSlots
        settings.UnavailableSlots = Context.Role switch
        {
            EntityRoleId.Profile => settings.UnavailableSlots,
            _ => null,
        };

        return settings;
    }

    [Authorize("default")]
    [HttpGet("/api/v1/Scheduler/Lead({id})/Slots")]
    public async Task<SlotsResp> GetSlotsByLeadAsync([FromRoute] Guid id, DateTime? start, DateTime? end, [FromQuery] bool includeUnavailable = false)
    {
        using var scope = logger.AddScope(new
        {
            LeadId = id,
        });

        logger.LogInformation("Get Slots for Lead/client");

        var lead = await schedulerService.GetLeadAsync(Context, id);
        var settings = await schedulerService.GetSettingsAsync(Context, lead);

        var timeZoneId = lead.TimeZoneId;
        if (string.IsNullOrEmpty(timeZoneId))
        {
            var entity = await connection.Filter<Entity>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Id, lead.EntityId)
                .FirstOrDefaultAsync();

            timeZoneId = entity.TimeZoneId;
        }

        var slots = includeUnavailable ?
            await schedulerService.GetUnavailableSlotsAsync(Context, settings, start, end, timeZoneId) :
            await schedulerService.GetSlotsAsync(Context, settings, start, end);

        var result = new SlotsResp
        {
            SuggestedSlots = schedulerService.CalculateSuggestedSlots(timeZoneId, slots),
            Slots = slots,
            TimeZoneId = timeZoneId,
            Users = await GetUserSettingsForOrganizationAsync(lead.EntityId, settings),
            AssignedEntityId = lead.AssignedEntityId,
        };

        return result;
    }

    private async Task<UserWithSchedulingSettings[]> GetUserSettingsForOrganizationAsync(Guid organizationId, SchedulerSettings settings)
    {
        var query = connection.Filter<Entity, PI.Shared.Models.User>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.OrganizationId, organizationId)
            .Ne(x => x.IsActive, false);

        var users = await query.FindAsync();
        var usersDict = users.ToDictionary(x => x.Id);
        var result = settings.Settings?
                .Select(x => usersDict.TryGetValue(x.UserId, out var user) ?
                    new UserWithSchedulingSettings
                    {
                        UserId = user.Id,
                        Name = user.Name,
                        AppointmentTypeId = x.AppointmentTypeId,
                    } :
                    null)
                .Where(x => x != null)
                .OrderBy(x => x.Name)
                .ToArray()
            ;

        return result;
    }

    [Authorize("default")]
    [HttpGet("/api/v1/Scheduler/Appointment({id})")]
    public async Task<ExtendedAppointment> GetAppointmentAsync([FromRoute] Guid id)
    {
        using var scope = logger.AddScope(new
        {
            AppointmentId = id,
        });

        logger.LogInformation("Get Appointment");

        var (appointment, lead) = await schedulerService.GetAppointmentAsync(Context, id);
        return await ConvertAsync(appointment, lead);
    }

    // [Obsolete("use form")]
    // [HttpDelete("/api/v1/Scheduler/Appointment")]
    // public async Task<ExtendedAppointment> CancelAppointmentAsync([FromBody] CancelAppointmentRequest request)
    // {
    //     using var scope = _logger.AddScope(new
    //     {
    //         LeadId = request?.ParentObjectId,
    //         request?.AppointmentId,
    //     });
    //
    //     _logger.LogInformation("Schedule appointment");
    //
    //     var (appointment, lead) = await _schedulerService.CancelAppointmentAsync(Context, request);
    //     if (appointment == null)
    //     {
    //         // was already cancelled?
    //         return null;
    //     }
    //
    //     return await ConvertAsync(appointment, lead);
    // }

    // /// <summary>
    // /// Used by the Appointment Field to schedule appointments 
    // /// </summary>
    // [Obsolete("use form")]
    // [HttpPost("Appointment")]
    // public async Task<ExtendedAppointment> ScheduleAppointmentAsync([FromBody] ScheduleAppointmentRequest request)
    // {
    //     using var scope = _logger.AddScope(new
    //     {
    //         LeadId = request?.ParentObjectId,
    //         request?.Start,
    //         request?.End,
    //         request?.AutoCancelPrevious,
    //         request?.RescheduleAppointmentId,
    //     });
    //
    //     _logger.LogInformation("Schedule appointment");
    //
    //     var (appointment, lead) = await _schedulerService.ScheduleAppointmentByUserAsync(Context, request);
    //
    //     return await ConvertAsync(appointment, lead);
    // }

    private async Task InitAsync(NewAppointmentState state)
    {
        state.Builder = await FormLayoutBuilder.NewAsync(connection, Context, state.AppointmentId, state.LeadId);
        if (!state.OrganizationId.HasValue) throw new BadRequestException("Missing Organization");

        state.Organization = await connection.Filter<Entity, PI.Shared.Models.Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, state.OrganizationId.Value)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (state.Organization == null) throw new BadRequestException("Invalid Organization");
        if (string.IsNullOrWhiteSpace(state.TimeZoneId)) throw new BadRequestException("Missing Time Zone");

        state.Settings = await schedulerService.GetSettingsAsync(Context, state.OrganizationId.Value);

        if (state.UserId.HasValue)
        {
            var setting = state.Settings?.Settings.FirstOrDefault(x => x.UserId == state.UserId.Value);
            if (setting != null)
            {
                state.AppointmentType = await connection.Filter<AppointmentType>()
                    .Eq(x => x.AccountId, state.Context.AccountId)
                    .Eq(x => x.Id, setting.AppointmentTypeId)
                    .FirstOrDefaultAsync();
            }
        }
    }

    [HttpGet("Appointment/DataForm")]
    [HttpGet("Appointment({id})/DataForm")]
    [HttpGet("Lead({leadId})/Appointment/DataForm")]
    [HttpGet("Lead({leadId})/Appointment({id})/DataForm")]
    public async Task<Form> ScheduleAppointmentForm([FromRoute] Guid? id, [FromRoute] Guid? leadId,
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? entityId,
        [FromQuery] DateTime? start,
        [FromQuery] bool? allowUnavailable,
        [FromQuery] Guid? appointmentId)
    {
        // fallback to query parameter
        id ??= appointmentId;

        if (Context.Role == EntityRoleId.User)
        {
            // users can only schedule for themselves
            entityId = Context.UserId.Value;
        }
        
        var state = new NewAppointmentState
        {
            Context = Context,
            AppointmentId = id,
            LeadId = leadId,
            OrganizationId = organizationId,
            UserId = entityId,
            AppointmentStart = start,
        };
        
        await InitAsync(state);

        var fields = newApptFields();
        var layout = newApptLayout();

        if (id.HasValue)
        {
            if (state.Builder.Appointment == null)
            {
                return Form.BuildErrorForm("Can't reschedule. Invalid Appointment");
            }

            fields = fields.Append(new TextField
            {
                Name = nameof(ScheduleAppointmentFormData.Notes),
                Label = "Reschedule Reason",
            });

            fields = fields.Append(
                new HiddenField
                {
                    Name = nameof(ScheduleAppointmentFormData.AppointmentId),
                    DefaultValue = id,
                }
            );

            layout = layout.Append(new[] { nameof(Appointment.Notes) });
        }

        var currentAppointment = state.Builder.GetObjectField();
        if (currentAppointment != null)
        {
            fields = fields.Append(currentAppointment);
            layout = layout.Append(new[] { currentAppointment.Name });
        }

        return new Form
        {
            Name = "ScheduleAppointment",
            Title = id.HasValue ? "Reschedule Appointment?" : "Schedule Appointment?",
            Fields = fields.ToArray(),
            Layouts = new BreakpointLayouts
            {
                ExtraSmall = GridFormLayout.New(ScreenBreakpoint.ExtraSmall, layout),
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Yes",
                    Label = "Yes",
                    Enable = new[] { Form.RequiredFieldsName }
                },
                new FormAction
                {
                    Name = FormAction.Client_Cancel,
                    Label = "No"
                }
            }
        };

        IEnumerable<FormField> newApptFields()
        {
            if (state.AllowChangingUser || entityId.HasValue)
            {
                yield return new ReferenceField
                {
                    Name = nameof(ScheduleAppointmentFormData.UserId),
                    Label = "User",
                    ReferenceFieldOptions = new ReferenceFieldOptions
                    {
                        ObjectType = nameof(User),
                        Criteria = new[]
                        {
                            Condition.Eq(nameof(Organization.IsActive), true),
                            Condition.Eq(nameof(PI.Shared.Models.User.OrganizationId), state.OrganizationId.Value),
                        },
                        Items = new Dictionary<string, string>
                        {
                            { Guid.Empty.ToString(), "Any Available" }
                        }
                    },
                    DefaultValue = entityId ?? state.UserId ?? Guid.Empty,
                    IsRequired = false,
                    Enable = !state.AllowChangingUser ? new[] { "false" } : null,
                };
            }

            yield return new ReferenceField
            {
                Name = nameof(ScheduleAppointmentFormData.LeadId),
                Label = "Lead",
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = nameof(Lead),
                    Criteria = new[]
                    {
                        Condition.Eq(nameof(Lead.IsActive), true),
                        Condition.Eq(nameof(Lead.EntityId), state.OrganizationId.Value),
                    },
                    Actions = state.LeadId.HasValue ?
                        null :
                        new[]
                        {
                            new FormAction
                            {
                                Name = FormAction.Client_New,
                                Label = "Create Lead...",
                            }
                        },
                },
                DefaultValue = state.LeadId,
                IsRequired = true,
                Enable = state.LeadId.HasValue ? new[] { "false" } : null,
            };

            yield return new DateField
            {
                Name = nameof(ScheduleAppointmentFormData.LocalDate),
                Label = "Local Date",
                IsRequired = true,
                DefaultValue = state.LocalDateStr,
                Enable = !state.IsUserRole ? new[] { "false" } : null,
            };

            yield return new TimeField
            {
                Name = nameof(ScheduleAppointmentFormData.LocalTime),
                Label = "Local Time",
                DefaultValue = state.LocalTimeStr,
                IsRequired = true,
                Enable = !state.IsUserRole ? new[] { "false" } : null,
            };

            // yield return new NumberField
            // {
            //     Name = nameof(ScheduleAppointmentFormData.Duration),
            //     NumberFieldOptions = new NumberFieldOptions
            //     {
            //         DecimalPlaces = 0,
            //     },
            //     DefaultValue = state.Duration,
            //     Visible = !state.IsUserRole ? new[] { "false" } : null,
            // };

            yield return new SelectField
            {
                Name = nameof(ScheduleAppointmentFormData.Duration),
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = new OrderedDictionary
                    {
                        { 0, "Default for User" },
                        { 30, "30 minutes" },
                        { 60, "1 hour" },
                        { 90, "90 minutes" },
                        { 120, "2 hours" },
                        { 150, "2 1/2 hours" },
                        { 180, "3 hours" },
                    },
                },
                DefaultValue = 0, // state.Duration
                Visible = !state.IsUserRole ? new[] { "false" } : null,
            };

            yield return new TextField
            {
                Name = nameof(ScheduleAppointmentFormData.TimeZoneId),
                Label = "Time Zone",
                DefaultValue = state.TimeZoneId,
                Enable = new[] { "false" },
            };

            yield return new TextField
            {
                Name = nameof(ScheduleAppointmentFormData.Name),
                Label = "Subject",
                DefaultValue = state.Subject,
            };

            yield return new CheckboxField
            {
                Name = nameof(ScheduleAppointmentFormData.SkipAvailabilityCheck),
                Label = "DO NOT enforce availability rules",
                DefaultValue = state.IsUserRole && allowUnavailable.GetValueOrDefault(false),
                Enable = !state.IsUserRole ? new[] { "false" } : null,
            };

            yield return new CheckboxField
            {
                Name = nameof(ScheduleAppointmentFormData.AllowUnavailableSlots),
                Label = "Request Unavailable Slot",
                DefaultValue = state.GetSkipAvailabilityValue(allowUnavailable),
                Enable = new[] { "false" },
            };
        }

        IEnumerable<IEnumerable<string>> newApptLayout()
        {
            yield return new[] { nameof(ScheduleAppointmentFormData.LeadId) };
            if (state.AllowChangingUser || entityId.HasValue) yield return new[] { nameof(ScheduleAppointmentFormData.UserId) };
            yield return new[] { nameof(ScheduleAppointmentFormData.LocalDate), nameof(ScheduleAppointmentFormData.LocalTime) };

            if (!state.IsUserRole)
            {
                // profile (e.g. callcenter)
                yield return new[] { nameof(ScheduleAppointmentFormData.TimeZoneId) };
                yield return new[] { nameof(ScheduleAppointmentFormData.AllowUnavailableSlots) };
            }
            else
            {
                // user
                yield return new[] { nameof(ScheduleAppointmentFormData.Duration), nameof(ScheduleAppointmentFormData.TimeZoneId) };
                yield return new[] { nameof(Appointment.Name) };
                yield return new[] { nameof(ScheduleAppointmentFormData.SkipAvailabilityCheck) };
            }
        }
    }

    [HttpPost("Appointment/DataForm")]
    [HttpPost("Appointment({id})/DataForm")]
    [HttpPost("Lead({leadId})/Appointment/DataForm")]
    [HttpPost("Lead({leadId})/Appointment({id})/DataForm")]
    public async Task<DataFormActionResponse> ScheduleAppointmentAsync([FromBody] DataFormActionRequest request)
    {
        if (request.Parameters == null) return new DataFormActionResponse(request, "Bad Request");
        var form = JsonConvert.DeserializeObject<ScheduleAppointmentFormData>(JsonConvert.SerializeObject(request.Parameters));

        if (form.UserId == Guid.Empty)
        {
            form.UserId = null;
        }

        if (Context.Role == EntityRoleId.User)
        {
            // users can only schedule for themselves
            form.UserId = Context.UserId.Value;
        }        
        
        if (string.IsNullOrWhiteSpace(form.TimeZoneId) || !form.LocalDate.HasValue || !form.LocalTime.HasValue) return new DataFormActionResponse(request, "Missing Start");

        var localDate = form.LocalDate.Value;
        var localtime = form.LocalTime.Value;
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(form.TimeZoneId);
        var start = TimeZoneInfo.ConvertTimeToUtc(new DateTime(localDate.Year, localDate.Month, localDate.Day, localtime.Hour, localtime.Minute, localtime.Second), timeZoneInfo);

        var allowSkipAvailabilityCheck = Context.Role switch
        {
            EntityRoleId.Admin or EntityRoleId.Manager => true,
            EntityRoleId.User => form.UserId.HasValue && Context.UserId.Value == form.UserId.Value,
            _ => false,
        };

        var skipAvailabilityCheck = allowSkipAvailabilityCheck && form.SkipAvailabilityCheck.GetValueOrDefault(false);

        var scheduleAppointmentRequest = new ScheduleAppointmentRequest
        {
            RescheduleAppointmentId = form.AppointmentId,
            ParentObjectId = form.LeadId,
            ParentObjectType = nameof(Lead),
            ParentFieldName = nameof(Lead.NextAppointmentId),
            UserId = form.UserId,
            AutoCancelPrevious = true,
            Start = start,
            End = allowSkipAvailabilityCheck && form.Duration.HasValue && form.Duration.Value > 0 ? start.Add(TimeSpan.FromMinutes(form.Duration.Value)) : null,
            Notes = form.Notes,
            SkipAvailabilityCheck = skipAvailabilityCheck,
            AllowUnavailableSlots = form.AllowUnavailableSlots.GetValueOrDefault(false),
        };

        using var scope = logger.AddScope(new
        {
            LeadId = scheduleAppointmentRequest.ParentObjectId,
            scheduleAppointmentRequest.Start,
            scheduleAppointmentRequest.End,
            scheduleAppointmentRequest.AutoCancelPrevious,
            scheduleAppointmentRequest.RescheduleAppointmentId,
            scheduleAppointmentRequest.UserId,
            scheduleAppointmentRequest.SkipAvailabilityCheck,
            scheduleAppointmentRequest.AllowUnavailableSlots,
        });

        logger.LogInformation("Schedule appointment By User");

        if (!scheduleAppointmentRequest.ParentObjectId.HasValue) return new DataFormActionResponse(request, "Missing Lead");

        var lead = await schedulerService.GetLeadAsync(Context, scheduleAppointmentRequest.ParentObjectId.Value);
        if (lead == null) return new DataFormActionResponse(request, "Invalid or missing lead");

        var result = await schedulerService.ScheduleAppointmentByUserAsync(Context, scheduleAppointmentRequest, lead);
        if (!result.IsSuccess) return new DataFormActionResponse(request, result.Status ?? "Slot not available");

        return new DataFormActionResponse(request, "Appointment scheduled", true)
        {
            NextUrl = FormAction.Client_Reload,
        };
    }

    [HttpGet("Appointment({id})/Cancel/DataForm")]
    public async Task<Form> AppointmentCancelForm([FromRoute] Guid? id)
    {
        // TODO: use form in the database
        // ...

        var builder = await FormLayoutBuilder.NewAsync(connection, Context, id);
        var validationError = builder.ValidateCancel();
        if (validationError != null) return Form.BuildErrorForm(validationError);

        var fields = new TextField
        {
            Name = nameof(Appointment.Notes),
            Label = "Cancellation Reason",
            TextFieldOptions = new TextFieldOptions
            {
                Multline = true
            }
        }.AsEnumerable<FormField>();

        var layout = new[] { nameof(Appointment.Notes) }.AsEnumerable<IEnumerable<string>>();

        var currentAppointment = builder.GetObjectField();
        if (currentAppointment != null)
        {
            fields = fields.Append(currentAppointment);
            layout = layout.Append(new[] { currentAppointment.Name });
        }

        return new Form
        {
            Name = "AppointmentCancel",
            Title = "Cancel Appointment?",
            Fields = fields.ToArray(),
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Yes",
                },
                new FormAction
                {
                    Name = FormAction.Client_Cancel,
                    Label = "No"
                }
            },
            Layouts = new BreakpointLayouts
            {
                ExtraSmall = GridFormLayout.New(ScreenBreakpoint.ExtraSmall, layout),
            }
        };
    }
    
    [HttpPost("Appointment/Cancel/DataForm")]
    [HttpPost("Appointment({id})/Cancel/DataForm")]
    public async Task<DataFormActionResponse> CancelAppointmentAsync([FromRoute] Guid? id, [FromBody] DataFormActionRequest request)
    {
        if (!id.HasValue && request.SelectedIds?.Length != 1) return new DataFormActionResponse(request, "Missing Appointment");
        id ??= request.SelectedIds.FirstOrDefault();
        request.SelectedIds = new[] { id.Value };

        var (appointment, lead) = await schedulerService.GetAppointmentAsync(Context, id.Value);
        var cancelRequest = new CancelAppointmentRequest
        {
            AppointmentId = id.Value,
            ParentObjectType = nameof(Lead),
            ParentObjectId = appointment.LeadId,
            ParentFieldName = nameof(Lead.NextAppointmentId),
            Notes = request.TryGetStrParam(nameof(Appointment.Notes), out var notes) ? notes : null,
        };

        (appointment, _) = await schedulerService.CancelAppointmentAsync(Context, cancelRequest, appointment, lead);
        if (appointment == null)
        {
            // was already cancelled?
            return new DataFormActionResponse(request, "Appointment was already cancelled");
        }

        return new DataFormActionResponse(request, "Appointment Cancelled", true)
        {
            NextUrl = FormAction.Client_Reload,
        };
    }

    /// <summary>
    /// uses standard form to add lead ... hack to allow redirecting to scheduler automatically afterwards 
    /// </summary>
    [HttpGet("Lead/DataForm")]
    public async Task<Form> GetAddFormAsync([FromServices] ObjectTypeService objectTypeService)
    {
        var result = await objectTypeService.GetAddDataFormAsync(Context, nameof(Lead));
        if (result == null) throw new NotFoundException();
        return result;
    }

    /// <summary>
    /// uses standard form to add lead ... hack to allow redirecting to scheduler automatically afterwards 
    /// </summary>
    [HttpPost("Lead/DataForm")]
    public async Task<DataFormActionResponse> EditFormOnActionAsync([FromBody] DataFormActionRequest request, [FromServices] ObjectTypeService objectTypeService)
    {
        if (request.Action != FormAction.Add) throw new ForbiddenException();
        var result = await objectTypeService.ExecObjectActionAsync(Context, nameof(Lead), request);
        if (result == null) throw new NotFoundException();
        if (!result.Success) return result;

        return new DataFormActionResponse(request)
        {
            NextUrl = $"page://Scheduler?LeadId={result.Ids[0]}",
            Success = true,
        };
    }

    private async Task<ExtendedAppointment> ConvertAsync(Appointment appointment, Lead lead)
    {
        var result = mapper.Map<ExtendedAppointment>(appointment);
        result.LeadName = lead?.Name;

        var entities = await connection.Filter<Entity>()
            .Eq(x => x.AccountId, appointment.AccountId)
            .In(x => x.Id, entityIds())
            .IncludeField(x => x.Name)
            .FindAsync();

        var dict = entities.ToDictionary(x => x.Id, x => x.Name);
        if (appointment.CreatedBy.HasValue && dict.TryGetValue(appointment.CreatedBy.Value, out var createdBy))
        {
            result.CreatedByName = createdBy;
        }

        if (appointment.CancelledBy.HasValue && dict.TryGetValue(appointment.CancelledBy.Value, out var cancelledBy))
        {
            result.CancelledByName = cancelledBy;
        }

        if (dict.TryGetValue(appointment.EntityId, out var entity))
        {
            result.EntityName = entity;
        }

        return result;

        IEnumerable<Guid> entityIds()
        {
            yield return appointment.EntityId;
            if (appointment.CreatedBy.HasValue) yield return appointment.CreatedBy.Value;
            if (appointment.CancelledBy.HasValue) yield return appointment.CancelledBy.Value;
        }
    }

    public class ScheduleAppointmentFormData
    {
        public Guid? AppointmentId { get; set; }
        public Guid? LeadId { get; set; }
        public Guid? UserId { get; set; }
        public DateTime? LocalDate { get; set; }
        public DateTime? LocalTime { get; set; }
        public int? Duration { get; set; }
        public string TimeZoneId { get; set; }
        public string Name { get; set; }
        public bool? AllowUnavailableSlots { get; set; }
        public bool? SkipAvailabilityCheck { get; set; }
        public string Notes { get; set; }
    }

    class FormLayoutBuilder
    {
        private readonly IEntityContext _context;
        private readonly MongoConnection _connection;
        private readonly Guid? _appointmentId;
        private Guid? _leadId;

        public Lead Lead { get; private set; }
        public Appointment Appointment { get; private set; }
        public Entity Entity { get; private set; }

        public static async Task<FormLayoutBuilder> NewAsync(MongoConnection connection, IEntityContext context, Guid? appointmentId = null, Guid? leadId = null)
        {
            var builder = new FormLayoutBuilder(connection, context, appointmentId, leadId);
            await builder.InitAsync();
            return builder;
        }

        private FormLayoutBuilder(MongoConnection connection, IEntityContext context, Guid? appointmentId = null, Guid? leadId = null)
        {
            _context = context;
            _connection = connection;
            _appointmentId = appointmentId;
            _leadId = leadId;
        }

        private async Task InitAsync()
        {
            if (_appointmentId.HasValue)
            {
                Appointment = await _connection.Filter<Appointment>()
                    .Eq(x => x.AccountId, _context.AccountId)
                    .Eq(x => x.Id, _appointmentId)
                    .FirstOrDefaultAsync();
            }

            _leadId ??= Appointment?.LeadId;
            if (_leadId.HasValue)
            {
                Lead = await _connection.Filter<Lead>()
                    .Eq(x => x.AccountId, _context.AccountId)
                    .Eq(x => x.Id, _leadId)
                    .FirstOrDefaultAsync();
            }

            if (Appointment != null)
            {
                Entity = await _connection.Filter<Entity>()
                    .Eq(x => x.AccountId, _context.AccountId)
                    .Eq(x => x.Id, Appointment.EntityId)
                    .FirstOrDefaultAsync();
            }
        }

        public string ValidateCancel()
        {
            if (Appointment == null) return "Appointment not found";
            if (!Appointment.IsActive || Appointment.CancelledOn.HasValue) return "Appointment has already been cancelled";
            if (Appointment.Start < DateTime.UtcNow) return "Can't cancel appointment in the past";

            if (Lead == null) return "Lead not found";

            var canAccess = _context.Role switch
            {
                EntityRoleId.Account or EntityRoleId.Admin or EntityRoleId.Profile => true,
                EntityRoleId.Manager or EntityRoleId.Organization => _context.OrganizationId == Lead.EntityId,
                EntityRoleId.User => Appointment.EntityId == _context.UserId,
                _ => false,
            };

            return !canAccess ? "Access forbidden" : null;
        }

        public ObjectField GetObjectField()
        {
            var fields = GetFields().ToArray();
            if (Appointment == null || fields.Length < 1) return null;

            var objectField = new ObjectField
            {
                Name = "CurrentAppointment",
                Label = "Current Appointment",
                ObjectFieldOptions = new ObjectFieldOptions
                {
                    ObjectType = nameof(Appointment),
                    EditForm = new Form
                    {
                        IsReadOnly = true,
                        Fields = fields,
                        Layouts = new BreakpointLayouts
                        {
                            ExtraSmall = GridFormLayout.New(ScreenBreakpoint.ExtraSmall, GetLayout()),
                        },
                    },
                },
                DefaultValue = fields.ToDictionary(x => x.Name, x => x.DefaultValue),
                Enable = new[] { "false" },
            };

            return objectField;
        }

        private IEnumerable<IEnumerable<string>> GetLayout()
        {
            if (Appointment != null)
            {
                yield return new[] { nameof(Appointment.Name) };
                yield return new[] { nameof(Appointment.LocalDate), nameof(Appointment.LocalTime) };
                yield return new[] { nameof(Appointment.TimeZoneId) };
            }

            if (Lead != null) yield return new[] { nameof(Appointment.LeadId) };
            if (Entity != null) yield return new[] { nameof(Appointment.EntityId) };
        }

        private IEnumerable<FormField> GetFields()
        {
            // new TagsField
            // {
            //     Name = nameof(Appointment.Tags),
            //     Label = "Tags",
            //     DefaultValue = appointment.Tags,
            //     Enable = new[] { "false" }
            // },

            if (Appointment != null)
            {
                yield return new TextField
                {
                    Name = nameof(Appointment.Name),
                    Label = "Appointment",
                    DefaultValue = Appointment.Name,
                    Enable = new[] { "false" }
                };

                yield return new TextField
                {
                    Name = nameof(Appointment.LocalDate),
                    Label = "Date",
                    DefaultValue = Appointment.LocalDate,
                    Enable = new[] { "false" }
                };

                yield return new TextField
                {
                    Name = nameof(Appointment.LocalTime),
                    Label = "Time",
                    DefaultValue = Appointment.LocalTime,
                    Enable = new[] { "false" }
                };

                yield return new TextField
                {
                    Name = nameof(Appointment.TimeZoneId),
                    Label = "Time Zone",
                    DefaultValue = Appointment.TimeZoneId,
                    Enable = new[] { "false" }
                };
            }

            if (Lead != null)
            {
                yield return new TextField
                {
                    Name = nameof(Appointment.LeadId),
                    Label = "Lead",
                    DefaultValue = Lead.Name,
                    Enable = new[] { "false" }
                };
            }

            if (Entity != null)
            {
                yield return new TextField
                {
                    Name = nameof(Appointment.EntityId),
                    Label = "User",
                    DefaultValue = Entity.Name,
                    Enable = new[] { "false" }
                };
            }
        }
    }

    class NewAppointmentState
    {
        public Guid? AppointmentId { get; init; }

        private Guid? _leadId;

        public Guid? LeadId
        {
            get => _leadId ??= Builder?.Appointment?.LeadId;
            set => _leadId = value;
        }

        private Guid? _organizationId;

        public Guid? OrganizationId
        {
            get => Context.OrganizationId ?? _organizationId ?? Builder.Lead?.EntityId;
            set => _organizationId = value;
        }

        public Guid? UserId { get; set; }
        public IEntityContext Context { get; init; }

        public FormLayoutBuilder Builder { get; set; }

        public bool IsUserRole => Context.Role switch
        {
            EntityRoleId.Admin or EntityRoleId.Manager or EntityRoleId.User => true,
            _ => false,
        };

        public bool AllowChangingUser => Context.Role switch
        {
            EntityRoleId.Admin or EntityRoleId.Manager => true,
            _ => false,
        };

        public bool AllowSkipAvailabilityCheck => IsUserRole switch
        {
            true => true,
            _ => Settings?.UnavailableSlots?.AssignEntityId.HasValue ?? false,
        };

        public string TimeZoneId => Builder.Lead?.TimeZoneId ?? Organization.TimeZoneId;

        private TimeZoneInfo _timeZoneInfo;
        public TimeZoneInfo TimeZoneInfo => _timeZoneInfo ??= TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

        public SchedulerSettings Settings { get; set; }
        public PI.Shared.Models.Organization Organization { get; set; }
        public DateTime? AppointmentStart { get; set; }

        private DateTime? _appointmentLocalStart;

        public DateTime? AppointmentLocalStart => _appointmentLocalStart ??= TimeZoneInfo.ConvertTimeFromUtc(AppointmentStart ?? Builder?.Appointment?.Start ?? DateTime.UtcNow, TimeZoneInfo);

        public string LocalDateStr => AppointmentLocalStart?.ToString("yyyy-MM-dd");
        public string LocalTimeStr => AppointmentLocalStart?.ToString("HH:mm");

        public AppointmentType AppointmentType { get; set; }
        public int? Duration => AppointmentType?.Settings?.Duration;

        public string Subject
        {
            get
            {
                var type = AppointmentType?.Description ?? AppointmentType?.Name;
                var lead = Builder?.Lead?.Name;
                return (type == null) ?
                    lead :
                    lead == null ? type : $"{lead} - {type}";
            }
        }

        public bool GetSkipAvailabilityValue(bool? allowUnavailable) => AllowSkipAvailabilityCheck && allowUnavailable.GetValueOrDefault(IsUserRole);
    }
}