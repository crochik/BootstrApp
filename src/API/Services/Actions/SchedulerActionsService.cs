using System.Dynamic;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Ical.Net.CalendarComponents;
using Messages.Flow;
using Newtonsoft.Json;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;
using Lead = PI.Shared.Models.Lead;
using Organization = PI.Shared.Models.Organization;

namespace Services;

public class SchedulerActionsService : AbstractMessageQueueService, ILifetimeService
{
    private readonly AppointmentSchedulerService _schedulerService;
    private readonly MongoConnection _connection;

    public SchedulerActionsService(
        ILogger<SchedulerActionsService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        AppointmentSchedulerService schedulerService,
        MongoConnection connection) :
        base(logger, configuration, messageBroker)
    {
        _schedulerService = schedulerService;
        _connection = connection;
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.CreateICal));
        mapper.Register<SimpleActionMessage<CreateICalActionOptions>>();

        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.SchedulerAvailability));
        mapper.Register<SimpleActionMessage<GenericActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var route = evt.RoutingKey.Split('.');
            if (!Guid.TryParse(route[1], out var actionId))
            {
                Logger.LogError("Unexpected {RoutingKey}", evt.RoutingKey);
                return;
            }

            switch (evt.Body)
            {
                case SimpleActionMessage<CreateICalActionOptions> createICal:
                    await CreateICalAsync(actionId, createICal);
                    break;

                case SimpleActionMessage<GenericActionOptions> action:
                    if (actionId == ActionIds.SchedulerAvailability)
                    {
                        await ProcessAvailabilityAsync(action);
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }

    private async Task ProcessAvailabilityAsync(SimpleActionMessage<GenericActionOptions> action)
    {
        if (action.Options is not GenericActionOptions genericActionOptions)
        {
            throw new Exception("Unexpected Options");
        }

        var options = genericActionOptions.ConvertTo<SchedulerAvailabilityActionOptions>();
        options.Output = genericActionOptions.Output;
        
        using var scope = Logger.BeginScope(new
        {
            action.Event.ObjectType,
            action.Event.TargetId
        });

        Logger.LogInformation("Calculate First Availability");

        Result<AppointmentMetaData> result;
        try
        {
            result = await ProcessAvailabilityAsync(action, options);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to calculate Availability");
            result = Result.Error<AppointmentMetaData>(ex.Message);
        }

        if (result.IsSuccess)
        {
            Logger.LogInformation("Successfully Calculated First Availability: {Name} {Date} {Time}", result.Status, result.Value.LocalDateStr, result.Value.LocalTimeStr);    
        }
        else
        {
            Logger.LogError("Failed to calculate first availability: {Status}", result.Status);
        }
        
        var outputName = result.IsSuccess ? SchedulerAvailabilityActionOptions.AvailableEvent : SchedulerAvailabilityActionOptions.NotAvailableEvent;
        var output = options.Output.FirstOrDefault(x => x.Name == outputName);

        if (output?.EventId.HasValue ?? false)
        {
            var evt = new GenericFlowEvent(action.Event)
            {
                Action = nameof(ActionIds.SchedulerAvailability),
                Description = output.Description,
                EventTypeId = output.EventId,
            };

            if (result.IsSuccess)
            {
                evt.SetMetaValue($"Action|Output|{nameof(AppointmentMetaData.LocalDateStr)}", result.Value.LocalDateStr);
                evt.SetMetaValue($"Action|Output|{nameof(AppointmentMetaData.LocalTimeStr)}", result.Value.LocalTimeStr);
                evt.SetMetaValue($"Action|Output|{nameof(AppointmentMetaData.Date)}", result.Value.Date);
                evt.SetMetaValue("Action|Output|Days", (int)(result.Value.Date-DateTime.UtcNow).TotalDays);
                evt.SetMetaValue("Action|Output|Name", result.Status);
                evt.SetMetaValue("Action|Output|Tags", result.Value.Tags);
            }

            await MessageBroker.DispatchAsync(evt);
        }        
    }

    private async Task<Result<AppointmentMetaData>> ProcessAvailabilityAsync(SimpleActionMessage<GenericActionOptions> action, SchedulerAvailabilityActionOptions options)
    {
        if (action.Event.ObjectType != nameof(Lead))
        {
            Logger.LogError("Action not implemented for {ObjectType}", action.Event.ObjectType);
            return Result.Error<AppointmentMetaData>("For now only handles Leads");
        }

        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.TargetId)
            .FirstOrDefaultAsync();

        if (lead == null)
        {
            Logger.LogError("Did not find {LeadId}", action.Event.TargetId);
            return Result.Error<AppointmentMetaData>("Lead not found");
        }
        
        var settings = await _schedulerService.GetSchedulerSettingsAsync(action.Event.AccountId, lead.EntityId, options.ClientId);
        if (settings == null || !settings.IsActive)
        {
            Logger.LogError("Scheduler settings not found for {OrganizationId}", lead.EntityId);
            return Result.Error<AppointmentMetaData>("Scheduler Settings not found");
        }

        var organization = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.Id, lead.EntityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (organization == null)
        {
            Logger.LogError("{OrganizationId} not found or inactive", lead.EntityId);
            return Result.Error<AppointmentMetaData>("Organization not found");
        }

        var start = options.MinOffset.HasValue ? DateTime.UtcNow + options.MinOffset.Value : default(DateTime?);
        var end = options.MaxOffset.HasValue ? DateTime.UtcNow + options.MaxOffset.Value : default(DateTime?);
        var timeZoneId = lead.TimeZoneId ?? organization.TimeZoneId;
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        var context = organization.Context;
        var slots = settings.UnavailableSlots != null && settings.UnavailableSlots.AssignEntityId.HasValue ? await _schedulerService.GetUnavailableSlotsAsync(context, settings, start, end, timeZoneId) : await _schedulerService.GetSlotsAsync(context, settings, start, end);

        var slot = slots.FirstOrDefault();
        if (slot == null)
        {
            Logger.LogInformation("No slots available");
            return Result.Error<AppointmentMetaData>("No slots available");
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(slot.Start, timezone);
        var namedSlot = AppointmentSchedulerService.GetNamedSlot(localNow, localTime, slot);

        var meta = AppointmentMetaData.Get(slot.Start, timeZoneId);
        
        return Result.Success(meta, namedSlot.Name);
    }

    private async Task CreateICalAsync(Guid eventId, SimpleActionMessage<CreateICalActionOptions> action)
    {
        using var scope = Logger.AddScope(new
        {
            EventId = eventId,
            action.Event.ObjectType,
            ObjectId = action.Event.TargetId,
            action.Event.RunId,
        });

        Logger.LogInformation("Create iCal for appointment");

        var appointment = await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.TargetId)
            .FirstOrDefaultAsync();

        if (appointment == null) throw NotFoundException.New<Appointment>(action.Event.TargetId);

        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.RunId)
            .IncludeFields(
                x => x.Objects,
                x => x.ObjectType,
                x => x.InitialEvent,
                x => x.InitialObject
            )
            .FirstOrDefaultAsync();

        if (flowRun == null) throw NotFoundException.New<FlowRun>(action.Event.RunId);

        var context = flowRun.BuildHandlebarsContext(action.Event);

        var calendar = new Ical.Net.Calendar();
        calendar.AddTimeZone(new VTimeZone(appointment.TimeZoneId));
        calendar.Method = action.Options.Method?.ToUpperInvariant() ?? "PUBLISH";
        calendar.Events.Add(new Ical.Net.CalendarComponents.CalendarEvent
        {
            Uid = $"{appointment.Id}@ProgramInterface.com",
            Summary = resolve(action.Options.Summary) ?? appointment.Name,
            Description = resolve(action.Options.Description),
            Start = new Ical.Net.DataTypes.CalDateTime(appointment.Start),
            End = new Ical.Net.DataTypes.CalDateTime(appointment.End),
        });

        var iCal = new Ical.Net.Serialization.CalendarSerializer().SerializeToString(calendar);
        var content = new MimeContent
        {
            Content = iCal,
            ContentType = $"text/calendar; method={calendar.Method}",
            Size = iCal.Length,
            Filename = "invite.ics",
        };

        // update flow run (could use the event meta data but ... )
        await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.RunId)
            .Update
            .Set(x => x.Objects["Appointment|iCal"], new ObjectWithType
            {
                ObjectType = nameof(MimeContent),
                Object = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(content)),
            })
            .UpdateOneAsync();

        var evt = new GenericFlowEvent(action.Event)
        {
            Description = "iCal generated",
            Action = nameof(ActionIds.CreateICal),
            EventTypeId = action.Options.NextEventId,
        };

        await MessageBroker.DispatchAsync(evt);

        string resolve(string token)
        {
            if (token == null) return null;
            if (!token.Contains("{{")) return token;
            return HandlebarsDotNet.Handlebars.Compile(token).Invoke(context);
        }
    }

    public class SchedulerAvailabilityActionOptions : ActionOptions
    {
        public const string AvailableEvent = "AvailableEvent";
        public const string NotAvailableEvent = "NotAvailableEvent";
        
        public string ClientId { get; set; }
        public TimeSpan? MinOffset { get; set; }
        public TimeSpan? MaxOffset { get; set; }
    }
}