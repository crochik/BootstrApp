using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Messaging;
using Crochik.Mongo;
using HandlebarsDotNet;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using MongoDB.Bson;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;
using Entity = PI.Shared.Models.Entity;
using User = PI.Shared.Models.User;

namespace Services;

public class ActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly O365Service _service;
    private readonly ObjectTypeService _objectTypeService;

    public ActionService(
        ILogger<ActionService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        O365Service service,
        ObjectTypeService objectTypeService
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _service = service;
        _objectTypeService = objectTypeService;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.O365ExportAppointment));
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.O365CancelAppointment));
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
                case SimpleActionMessage<GenericActionOptions> msg:
                {
                    if (actionId == ActionIds.O365ExportAppointment)
                    {
                        await ProcessCreateAsync(msg);
                    }
                    else if (actionId == ActionIds.O365CancelAppointment)
                    {
                        await ProcessCancelAsync(msg);
                    }

                    break;
                }
            }
        }
        finally
        {
            evt.Acknowledge();
        }
    }

    private async Task ProcessCancelAsync(SimpleActionMessage<GenericActionOptions> action)
    {
        if (action.Options is not GenericActionOptions genericActionOptions)
        {
            Logger.LogError("Unexpected Options");
            return;
        }

        var options = genericActionOptions.ConvertTo<CancelAppointmentActionOptions>();
        options.Output = genericActionOptions.Output;

        var result = await ProcessMessageAsync(action.Event, options);
        if (result.IsUnknown)
        {
            Logger.LogInformation("Skipped: {Status}", result.Status);
            // TODO: should fire skip event?
            // ...
            return;
        }
    }

    private async Task ProcessCreateAsync(SimpleActionMessage<GenericActionOptions> action)
    {
        if (action.Options is not GenericActionOptions genericActionOptions)
        {
            Logger.LogError("Unexpected Options");
            return;
        }

        var options = genericActionOptions.ConvertTo<ExportAppointmentActionOptions>();
        options.Output = genericActionOptions.Output;

        var result = await ProcessMessageAsync(action.Event, options);
        if (result.IsUnknown)
        {
            Logger.LogInformation("Skipped: {Status}", result.Status);
            // TODO: should fire skip event?
            // ...
            return;
        }

        var outputName = result.IsSuccess ? ExportAppointmentActionOptions.CreatedEventOutputName : ExportAppointmentActionOptions.FailedToCreateOutputName;
        var output = options.Output.FirstOrDefault(x => x.Name == outputName);

        if (output?.EventId.HasValue ?? false)
        {
            var successEvt = new GenericFlowEvent(action.Event)
            {
                Action = nameof(ActionIds.O365ExportAppointment),
                Description = output.Description,
                EventTypeId = output.EventId,
            };

            if (result.IsSuccess)
            {
                successEvt.AddRefValue(O365Event.ObjectTypeName, result.Value.Id);
                successEvt.SetMetaValue($"Action|Output|{O365Event.ObjectTypeName}Id", result.Value.Id);
            }

            await MessageBroker.DispatchAsync(successEvt);
        }
    }

    private async Task<Result<O365Event[]>> ProcessMessageAsync(FlowEvent evt, CancelAppointmentActionOptions options)
    {
        Logger.LogInformation("Received Cancel Appointment action");

        var accountContext = new AccountContext(evt.AccountId);
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.Id, evt.RunId)
            .FirstOrDefaultAsync();

        var runContext = flowRun.BuildHandlebarsContext(evt);

        var criteria = new List<Condition>();
        foreach (var condition in options.Criteria.Conditions)
        {
            if (condition.Value is not string criteriaValue)
            {
                criteria.Add(Condition.New(condition.FieldName, condition.Operator, condition.Value));
                continue;
            }

            if (!ExpressionEvaluatorService.TryResolve(accountContext, runContext, criteriaValue, out var value))
            {
                Logger.LogError("Couldn't resolve {Expression} in Criteria", criteriaValue);
                return Result.Error<O365Event[]>("Couldn't resolve expression");
            }

            criteria.Add(Condition.New(condition.FieldName, condition.Operator, value));
        }

        var objectType = await _objectTypeService.GetAsync(accountContext, O365Event.ObjectTypeName);
        if (objectType == null)
        {
            return Result.Error<O365Event[]>("o365.Event object missing");
        }

        var matches = await _objectTypeService.FindAsync<O365Event>(accountContext, objectType, criteria.ToArray(), options.AllowMultiple ? 0 : 2);
        if (matches.Count < 1)
        {
            return Result.Error<O365Event[]>("No matches");
        }

        if (!options.AllowMultiple && matches.Count > 1)
        {
            return Result.Error<O365Event[]>("More than one match");
        }
        
        var list = new List<O365Event>();
        foreach (var o365Event in matches)
        {
            var result = await CancelAsync(accountContext, o365Event, options.Operation == CancelAppointmentActionOptions.OperationOptions.Delete);
            if (result.IsError) return result.ConvertTo<O365Event[]>();
            if (result.IsSuccess)
            {
                list.Add(result.Value);
            }
        }

        return list.Count > 0 ? Result.Success(list.ToArray()) : Result.Unknown<O365Event[]>("Nothing to cancel");
    }

    private async Task<Result<O365Event>> CancelAsync(IEntityContext context, O365Event evt, bool delete = false)
    {
        if (evt.IsCancelled.GetValueOrDefault())
        {
            Logger.LogInformation("{O365EventId} has already been cancelled, skip", evt.Id);
            return Result.Unknown<O365Event>("Event has already been cancelled");
        }

        if (!delete)
        {
            // TODO: implement cancel only 
            // ...
        }

        var user = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, evt.EntityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return Result.Error<O365Event>("Appointment owner is not active");
        }

        return await _service.DeleteEventFromCalendarAsync(user.Context, evt);
    }

    private async Task<Result<O365Event>> ProcessMessageAsync(FlowEvent evt, ExportAppointmentActionOptions options)
    {
        Logger.LogInformation("Received Export Appointment action");

        var accountContext = new AccountContext(evt.AccountId);
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.Id, evt.RunId)
            .FirstOrDefaultAsync();

        var runContext = flowRun.BuildHandlebarsContext(evt);

        if (!ExpressionEvaluatorService.TryResolve(accountContext, runContext, options.UserId, out var userIdObj))
        {
            Logger.LogError("Couldn't resolve user: {UserId}", options.UserId);
            return Result.Error<O365Event>("Couldn't resolve user");
        }

        if (!userIdObj.TryToParseObjectId(out var userId))
        {
            Logger.LogError("Invalid {UserId}", userIdObj);
            return Result.Error<O365Event>("Invalid User");
        }

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, accountContext.AccountId.Value)
            .Eq(x => x.Id, userId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            Logger.LogError("{UserId} is not active", userId);
            return Result.Error<O365Event>("User not found or Inactive");
        }

        var context = user.Context;
        var integrationConfiguration = await _service.GetIntegrationConfigurationAsync(context);
        if (!integrationConfiguration.IsSuccess)
        {
            Logger.LogInformation("Integration not configured/found for {UserId}: {Status}", userId, integrationConfiguration.Status);
            return Result.Unknown<O365Event>("Integration not configured");
        }

        if (!integrationConfiguration.Value.ExportAppointments)
        {
            Logger.LogInformation("{EntityId} Opted out of exporting appointments, nothing to do", integrationConfiguration.Value.EntityId);
            return Result.Unknown<O365Event>("Integration not configured to export appointments");
        }

        if (!ExpressionEvaluatorService.TryResolve(context, runContext, options.Subject, out var subjectObj) || subjectObj is not string subject)
        {
            Logger.LogError("Failed to resolve {Subject}", options.Subject);
            return Result.Error<O365Event>("Couldn't resolve subject");
        }

        var start = resolveDate(options.Start);
        var end = resolveDate(options.End);
        if (!start.HasValue || !end.HasValue)
        {
            Logger.LogError("Failed to resolve start/end");
            return Result.Error<O365Event>("Couldn't resolve start and/or end");
        }

        var body = Handlebars.Compile(options.Content).Invoke(runContext);

        // TODO: make them into options?
        // ...
        var appointmentId = evt.ObjectType == nameof(Appointment) ? evt.TargetId : default(Guid?);
        var reminderMinutesBeforeStart = 15;

        var location = default(Location);
        var value = default(object);
        if (ExpressionEvaluatorService.TryResolve(context, runContext, options.Address, out value) && value is string address)
        {
            location ??= new Location
            {
                LocationType = LocationType.StreetAddress
            };
            location.Address ??= new PhysicalAddress();
            location.Address.Street = address;

            if (ExpressionEvaluatorService.TryResolve(context, runContext, options.City, out value) && value is string city)
            {
                location.Address.City = city;
            }

            if (ExpressionEvaluatorService.TryResolve(context, runContext, options.State, out value) && value is string state)
            {
                location.Address.State = state;
            }

            if (ExpressionEvaluatorService.TryResolve(context, runContext, options.PostalCode, out value) && value is string postalCode)
            {
                location.Address.PostalCode = postalCode;
            }

            if (ExpressionEvaluatorService.TryResolve(context, runContext, options.Country, out value) && value is string country)
            {
                location.Address.CountryOrRegion = country;
            }

            location.DisplayName = string.Join(", ", getAddress().Where(x => !string.IsNullOrWhiteSpace(x)));

            IEnumerable<string> getAddress()
            {
                yield return location.Address.Street;
                yield return location.Address.City;
                yield return location.Address.PostalCode;
                yield return location.Address.State;
                yield return location.Address.CountryOrRegion;
            }
        }

        // if (ExpressionEvaluatorService.TryResolve(context, runContext, options.LinkUrl, out value) && value is string uri)
        // {
        //     location ??= new Location();
        //     location.LocationUri = uri;
        // }

        var timeZoneId = user.TimeZoneId;
        var req = new Event
        {
            ShowAs = Microsoft.Graph.FreeBusyStatus.Busy,
            Type = Microsoft.Graph.EventType.SingleInstance,
            IsAllDay = false,
            Location = location,
            IsReminderOn = reminderMinutesBeforeStart > 0,
            ReminderMinutesBeforeStart = reminderMinutesBeforeStart,
            Subject = subject,
            Start = new DateTimeTimeZone
            {
                TimeZone = timeZoneId,
                DateTime = TimeZoneInfo
                    .ConvertTimeBySystemTimeZoneId(start.Value, timeZoneId)
                    .ToString("o")
            },

            End = new DateTimeTimeZone
            {
                TimeZone = timeZoneId,
                DateTime = TimeZoneInfo
                    .ConvertTimeBySystemTimeZoneId(end.Value, timeZoneId)
                    .ToString("o")
            },

            Body = new ItemBody
            {
                ContentType = options.ContentType switch
                {
                    "text/html" => BodyType.Html,
                    _ => BodyType.Text,
                },
                Content = body
            },

            // IsOnlineMeeting = location == null, // ???
            // OnlineMeetingInfo
            // OnlineMeetingProvider
            // OnlineMeetingUrl

            IsOrganizer = true,
            // Importance
            // Sensitivity
            // IsOrganizer = true,
            // Extensions
            // Attachments
            // Organizer
            // SingleValueExtendedProperties
            // MultiValueExtendedProperties
        };

        var o365Event = await _service.CreateCalendarEventAsync(context, req, appointmentId);
        if (!o365Event.IsSuccess) return o365Event;

        var objectType = await _objectTypeService.GetAsync(context, O365Event.ObjectTypeName);
        if (objectType != null)
        {
            await _objectTypeService.AddObjectToFlowRunAsync(context, objectType, o365Event.Value.Id, flowRun, options.Alias);
        }

        return o365Event;

        DateTime? resolveDate(string value)
        {
            if (!ExpressionEvaluatorService.TryResolve(context, runContext, value, out var dateObj))
            {
                return null;
            }

            return dateObj switch
            {
                DateTime dt => dt,
                string str => DateTime.TryParse(str, out var dt) ? dt : default(DateTime?),
                _ => null,
            };
        }
    }
}