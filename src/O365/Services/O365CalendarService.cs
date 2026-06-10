using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using PI.Shared.App;
using PI.Shared.Models;
using PI.Shared.Services;
using EmailAddress = Microsoft.Graph.EmailAddress;
using Entity = PI.Shared.Models.Entity;
using User = PI.Shared.Models.User;

namespace Services;

public class O365CalendarService : AbstractMessageQueueService, ILifetimeService
{
    /// <summary>
    /// Route used for events 
    /// </summary>
    private const string EventRoute = "o365.event";

    private readonly ILogger<O365CalendarService> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly O365Service _o365Service;

    public O365CalendarService(
        IConfiguration configuration,
        ILogger<O365CalendarService> logger,
        MongoConnection connection,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        ObjectTypeService objectTypeService,
        O365Service o365Service
    ) : base(logger, configuration, messageBroker)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _o365Service = o365Service;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        // MessageBroker.Bind(queue, "appointment.*.cancel");
        // MessageBroker.Bind(queue, "appointment.*.add");
        // mapper.Register<Messages.Lead.AppointmentEvent>();

        // MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.ExportAppointmentToOffice365));
        // mapper.Register<Messages.Flow.ExportAppointmentToOffice365Action.Message>();

        MessageBroker.Bind(queue, EventRoute);
        mapper.Register<O365EventNotification>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                // event update from o365 webhook
                case O365EventNotification notification:
                    await ProcessAsync(notification);
                    break;

                // case AppointmentEvent appt:
                //     if (evt.RoutingKey.EndsWith(".add", StringComparison.Ordinal))
                //     {
                //         await AddAppointmentAsync(appt);
                //     }
                //     else if (evt.RoutingKey.EndsWith(".cancel", StringComparison.Ordinal))
                //     {
                //         await DeleteAppointmentAsync(appt);
                //     }
                //     else
                //     {
                //         Logger.LogError("Unexpected {rountingKey}", evt.RoutingKey);
                //     }
                //
                //     break;
                //
                // case ExportAppointmentToOffice365Action.Message flow:
                //     await ExportAppointmentAsync(flow);
                //     break;

                default:
                    Logger.LogError("Unexpected Message Type: {type}", evt.BodyType);
                    break;
            }
        }
        catch (Exception ex)
        {
            // TODO: how to handle failures?
            // resubmit? 
            // ... 

            Logger.LogError(ex, "Failed to process message: {type}", evt.BodyType);
        }

        evt.Acknowledge();
    }

    /// <summary>
    /// Publish event notifications from o365 webhook
    /// </summary>
    public async Task PublishAsync(IEnumerable<O365EventNotification> notifications)
    {
        foreach (var notification in notifications)
        {
            await MessageBroker.PublishAsync(EventRoute, notification);
        }
    }

    /// <summary>
    /// Process notification from o365
    /// </summary>
    public async Task ProcessAsync(O365EventNotification n)
    {
        using var scope = _logger.AddScope(new
        {
            n.UserId,
            n.AccountId,
            n.Resource,
            n.ChangeType,
        });

        _logger.LogInformation("Process Notification");

        if (!n.UserId.HasValue)
        {
            // nothing to do for now?
            // ...
            return;
        }

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, n.AccountId)
            .Eq(x => x.Id, n.UserId.Value)
            .FirstOrDefaultAsync();

        if (user == null || !user.IsActive)
        {
            _logger.LogError("User not found or not active");
            return;
        }

        var parts = n.Resource.Split('/');
        if (parts.Length != 4 || !parts[0].Equals("Users"))
        {
            _logger.LogError("Unexpected Resource");
            return;
        }

        var resourceId = parts[3];

        switch (parts[2])
        {
            case "Events":
                await ProcessCalendarEventAsync(user, resourceId, n);
                break;

            case "Messages":
                try
                {
                    await ProcessMessageAsync(user, resourceId, n);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process {MessageId}", resourceId);
                }

                break;

            default:
                _logger.LogError("Unhandled Resource");
                break;
        }
    }

    private async Task ProcessMessageAsync(User user, string resourceId, O365EventNotification notification)
    {
        var context = user.Context;

        var integrationResult = await _o365Service.GetIntegrationConfigurationAsync(context);

        if (!integrationResult.IsSuccess)
        {
            _logger.LogError("Failed to get integration, ignore event: {Status}", integrationResult.Status);
            return;
        }

        var integration = integrationResult.Value;

        // var accessToken = await _dataProtectionService.UnprotectAsync(
        //     context,
        //     new MicrosoftDataProtectionConfig
        //     {
        //         Purpose = CCIntegrationConfiguration.ProtectionKey,
        //     },
        //     integration.PersonalAccessToken
        // );

        var message = await _o365Service.GetMessageAsync(context, resourceId);

        if (message.IsDraft ?? false)
        {
            _logger.LogInformation("Message is a draft, ignore");
            return;
        }

        var user365 = await _o365Service.GetUserAsync(context);

        var outbound = null != user365.OtherMails
            .Append(user365.Mail)
            .Append(user.Email)
            .FirstOrDefault(x => x != null && string.Equals(x, message.Sender.EmailAddress.Address, StringComparison.OrdinalIgnoreCase));

        if (outbound && (message.ToRecipients == null || message.ToRecipients.IsEmpty()))
        {
            _logger.LogInformation("Outbound email without recipients, for now just ignore");
            return;
        }

        var note = new CommunicationNote
        {
            AccountId = user.AccountId,
            EntityId = user.OrganizationId.Value, // or org?
            Id = Guid.NewGuid(),
            Name = outbound ? $"Sent Email to {contact(message.ToRecipients.First().EmailAddress)}" : $"Received Email from {contact(message.Sender.EmailAddress)}",
            Provider = "Microsoft",
            ExternalId = message.Id,
            ExternalUrl = message.WebLink,
            Description = getDescription(),
            Status = outbound ? "Sent" : (message.IsRead ?? false ? "Read" : "Received"),
            CreatedOn = ((outbound ? message.SentDateTime : message.ReceivedDateTime) ?? message.CreatedDateTime)?.UtcDateTime ?? DateTime.UtcNow,
            Direction = outbound ? CommunicationDirection.Outbound : CommunicationDirection.Inbound,
            CommunicationChannel = CommunicationChannel.Email,
            Parties = getParties().ToArray(),
            Refs = getRefs().ToList(),
            Milestones = new Dictionary<string, DateTime>(milestones()),
            Meta = new Dictionary<string, object>(meta()),
        };

        var now = DateTime.UtcNow;

        var update = _connection.Filter<CommunicationNote>()
                .Eq(x => x.AccountId, note.AccountId)
                .Eq(x => x.EntityId, note.EntityId)
                .Eq(x => x.Provider, note.Provider)
                .Eq(x => x.ExternalId, note.ExternalId)
                .Update
                .SetOnInsert("_t", "communication")
                .SetOnInsert(x => x.Id, note.Id)
                .SetOnInsert(x => x.AccountId, note.AccountId)
                .SetOnInsert(x => x.Provider, note.Provider)
                .SetOnInsert(x => x.ExternalId, note.ExternalId)
                .SetOnInsert(x => x.ExternalUrl, note.ExternalUrl)
                .SetOnInsert(x => x.EntityId, note.EntityId)
                .SetOnInsert(x => x.Name, note.Name)
                .SetOnInsert(x => x.Description, note.Description)
                .SetOnInsert(x => x.Direction, note.Direction)
                .SetOnInsert(x => x.CommunicationChannel, note.CommunicationChannel)
                .SetOnInsert(x => x.CreatedOn, note.CreatedOn)
                .SetOnInsert(x => x.Refs, note.Refs)
                .SetOnInsert(x => x.Parties, note.Parties)
                .Set(x => x.LastModifiedOn, now)
                .Set(x => x.Status, note.Status)
            ;

        foreach (var milestoneKvp in note.Milestones)
        {
            update.Set(x => x.Milestones[milestoneKvp.Key], milestoneKvp.Value);
        }

        foreach (var metaKvp in note.Meta)
        {
            update.Set(x => x.Meta[metaKvp.Key], metaKvp.Value);
        }

        if (integration.CaptureBody && message.Body?.Content != null)
        {
            update.SetOnInsert(x => x.Content, message.Body.Content)
                .SetOnInsert(x => x.ContentType, message.Body.ContentType switch
                {
                    BodyType.Html => "text/html",
                    _ => "text/plain",
                })
                ;
        }

        if (integration.MessageFlowId.HasValue) update.SetOnInsert(x => x.FlowId, integration.MessageFlowId.Value);
        if (integration.MessageObjectStatusId.HasValue) update.SetOnInsert(x => x.ObjectStatusId, integration.MessageObjectStatusId.Value);

        var result = await update.UpdateAndGetOneAsync(true);
        if (result.Id != note.Id)
        {
            // modified
            _logger.LogInformation("{CommunicationNote} updated", result.Id);

            await _objectTypeService.FireObjectUpdatedAsync(context, result, new Dictionary<string, object>
            {
                { nameof(CommunicationNote.Milestones), "*" },
                { nameof(CommunicationNote.Meta), "*" },
                { nameof(CommunicationNote.Status), note.Status },
            });
        }
        else
        {
            // created (first time) 
            _logger.LogInformation("{CommunicationNoteId} Added", result.Id);

            await _objectTypeService.FireCreateEventAsync(context, result);
        }

        return;
        
        string getDescription()
        {
            return string.Join('\n', lines());

            IEnumerable<string> lines()
            {
                yield return $"Subject: {message.Subject}";
                if (message.From?.EmailAddress != null)
                {
                    yield return $"From: {contact(message.From.EmailAddress)}";
                }

                if (message.CcRecipients != null && !message.CcRecipients.IsEmpty())
                {
                    yield return $"CC: {string.Join(", ", message.CcRecipients.Select(x => contact(x.EmailAddress)))}";
                }

                if (message.BccRecipients != null && !message.BccRecipients.IsEmpty())
                {
                    yield return $"BCC: {string.Join(", ", message.BccRecipients.Select(x => contact(x.EmailAddress)))}";
                }

                if (integration.CaptureBody && !string.IsNullOrWhiteSpace(message.BodyPreview)) yield return $"\n{message.BodyPreview}";
            }
        }

        IEnumerable<KeyValuePair<string, object>> meta()
        {
            yield return new(nameof(Message.Subject), message.Subject);
            if (!outbound && message.From?.EmailAddress?.Address != null)
            {
                yield return new(nameof(Message.From), new Dictionary<string, object>
                {
                    { nameof(EmailAddress.Name), message.From.EmailAddress.Name },
                    { nameof(EmailAddress.Address), message.From.EmailAddress.Address },
                });
            }

            yield return new("ParentFolderId", message.ParentFolderId);
            yield return new("ConversationId", message.ConversationId);
        }

        IEnumerable<KeyValuePair<string, object>> getRefs()
        {
            yield return new($"{nameof(User)}Id", user.Id);
            yield return new("ConversationId", message.ConversationId);

            if (outbound)
            {
                foreach (var r in getRecipients())
                {
                    yield return new("Email", Lead.GetNormalizedEmail(r.EmailAddress.Address));
                }
            }
            else
            {
                yield return new($"Email", Lead.GetNormalizedEmail(message.From.EmailAddress.Address));
            }
        }

        IEnumerable<KeyValuePair<string, DateTime>> milestones()
        {
            if (message.CreatedDateTime.HasValue) yield return new KeyValuePair<string, DateTime>(CommunicationNote.CreatedStatus, message.CreatedDateTime.Value.UtcDateTime);
            if (!outbound && message.ReceivedDateTime.HasValue) yield return new KeyValuePair<string, DateTime>(CommunicationNote.ReceivedStatus, message.ReceivedDateTime.Value.UtcDateTime);
            if (outbound && message.SentDateTime.HasValue) yield return new KeyValuePair<string, DateTime>(CommunicationNote.SentStatus, message.SentDateTime.Value.UtcDateTime);
        }

        IEnumerable<CommunicationParty> getParties()
        {
            yield return new CommunicationParty
            {
                Direction = CommunicationDirection.Outbound,
                Address = message.Sender.EmailAddress.Address,
            };

            foreach (var recipient in getRecipients())
            {
                yield return new CommunicationParty
                {
                    Direction = CommunicationDirection.Inbound,
                    Address = recipient.EmailAddress.Address,
                };
            }
        }

        IEnumerable<Recipient> getRecipients() => (message.ToRecipients ?? Enumerable.Empty<Recipient>())
            .Concat(message.CcRecipients ?? Enumerable.Empty<Recipient>())
            .Concat(message.BccRecipients ?? Enumerable.Empty<Recipient>());

        string contact(EmailAddress address)
        {
            return string.IsNullOrWhiteSpace(address.Name) ? address.Address : $"{address.Name} <{address.Address}>";
        }
    }

    private async Task ProcessCalendarEventAsync(User user, string resourceId, O365EventNotification notification)
    {
        if (notification.ChangeType.Equals("deleted"))
        {
            await _o365Service.OnGraphEventDeletedAsync(user.Context, resourceId);
        }
        else
        {
            await _o365Service.OnGraphEventUpdatedAsync(user.Context, resourceId);
        }

        _logger.LogDebug("{AccountId} {UserId} {ChangeType} {Resource}", notification.AccountId, notification.UserId, notification.ChangeType, notification.Resource);
    }

    // private async Task ExportAppointmentAsync(ExportAppointmentToOffice365Action.Message action)
    // {
    //     var apptInfo = action.Event.Appointment;
    //
    //     bool success;
    //     string externalId;
    //     string status;
    //     Event evt;
    //
    //     if (action.Event.Appointment.Appointment.CancelledOn.HasValue)
    //     {
    //         // delete
    //         (success, externalId, status) = await DeleteAppointmentAsync(apptInfo.Appointment, apptInfo.IntegrationMapping);
    //     }
    //     else
    //     {
    //         // add appointment
    //         (evt, status) = await AddAppointmentAsync(apptInfo.Appointment, action.Event.Context.EntityId);
    //         externalId = evt?.Id;
    //         success = evt != null;
    //     }
    //
    //     if (action.Options.NextEventId.HasValue)
    //     {
    //         await MessageBroker.PublishAppointmentEventAsync(
    //             action.Event,
    //             nextEventId: action.Options.NextEventId.Value,
    //             integrationId: IntegrationIds.Office365,
    //             externalId: externalId,
    //             status: status,
    //             failed: !success
    //         );
    //     }
    // }

    // private async Task DeleteAppointmentAsync(AppointmentEvent appt)
    // {
    //     var (success, externalId, status) = await DeleteAppointmentAsync(appt.Appointment, appt.IntegrationMapping);
    //     if (string.IsNullOrEmpty(externalId)) return;
    //
    //     // publish message
    //     var message = new AppointmentExported
    //     {
    //         Id = appt.Appointment.Id,
    //         IntegrationId = IntegrationIds.Office365,
    //         ExternalId = externalId,
    //         CurrentState = AppointmentExported.State.Deleted,
    //         Status = status,
    //         // Url = evt.WebLink,
    //         // Data = new { evt.Id, evt.ICalUId, evt.WebLink },
    //         IsCalendar = true
    //     };
    //
    //     await MessageBroker.PublishAsync(
    //         $"appointment.{appt.Appointment.AppointmentTypeId}.exported",
    //         message
    //     );
    // }

    // private async Task<(bool success, string externalId, string status)> DeleteAppointmentAsync(Appointment appt, IEnumerable<IntegrationMapping> integrationMapping)
    // {
    //     using var apm = ApmService.StartTransaction("O365", "Delete Event", "Event", "Delete");
    //     apm.Context = new
    //     {
    //         AppointmentId = appt.Id
    //     };
    //
    //     // find eventid 
    //     string externalId = null;
    //     foreach (var i in integrationMapping)
    //     {
    //         if (i.IntegrationId == IntegrationIds.Office365)
    //         {
    //             externalId = i.ExternalId;
    //             break;
    //         }
    //     }
    //
    //     if (externalId == null)
    //     {
    //         Logger.LogInformation("Cancelled Appointment but eventid not found to delete from O365: {appointmentId} to {userId}", appt.Id, appt.EntityId);
    //         return (false, null, "Missing EventId");
    //     }
    //
    //     Logger.LogInformation("Cancelled Appointment, delete {eventId} from O365: {appointmentId} to {userId}", externalId, appt.Id, appt.EntityId);
    //
    //     var status = "Removed from Calendar";
    //     try
    //     {
    //         await RemoveAppointmentFromCalendarAsync(appt);
    //     }
    //     catch (Exception ex)
    //     {
    //         Logger.LogError(ex,
    //             "Error removing event from calendar: {userId} {appointmentId} {eventId}",
    //             appt.EntityId, appt.Id, externalId);
    //
    //         status = $"Failed to remove: {ex.Message}";
    //         return (false, externalId, status);
    //     }
    //
    //     return (true, externalId, status);
    // }

    // private async Task AddAppointmentAsync(AppointmentEvent appt)
    // {
    //     var (evt, status) = await AddAppointmentAsync(appt.Appointment, appt.ScheduledBy);
    //
    //     AppointmentExported message = new AppointmentExported
    //     {
    //         Id = appt.Appointment.Id,
    //         IntegrationId = IntegrationIds.Office365,
    //         ExternalId = evt?.Id ?? appt.Appointment.Id.ToString(),
    //         CurrentState = AppointmentExported.State.Added, // ???
    //         Status = status,
    //         Url = evt?.WebLink,
    //         Data = evt == null ? new { Error = status } : new { evt.Id, evt.ICalUId, evt.WebLink },
    //         IsCalendar = (evt != null) // don't flag the appointment as exported if failed
    //     };
    //
    //     await MessageBroker.PublishAsync(
    //         $"appointment.{appt.Appointment.AppointmentTypeId}.exported",
    //         message
    //     );
    // }

    // private async Task<(Event evt, string status)> AddAppointmentAsync(Appointment appt, Guid? scheduledBy)
    // {
    //     using var apm = ApmService.StartTransaction("O365", "Add Event", "Event", "Add");
    //     apm.Context = new
    //     {
    //         AppointmentId = appt.Id,
    //         UserId = appt.EntityId,
    //         ScheduledBy = scheduledBy
    //     };
    //
    //     Logger.LogInformation("Add Appointment to O365: {appointmentId} to {userId}", appt.Id, appt.EntityId);
    //
    //     try
    //     {
    //         var evt = await _o365Service.AddAppointmentToCalendarAsync(appt);
    //         return (evt, "Added to Calendar");
    //     }
    //     catch (Exception ex)
    //     {
    //         Logger.LogError(ex,
    //             "Error adding event from calendar: {userId} {appointmentId}",
    //             appt.EntityId, appt.Id);
    //
    //         return (null, $"Failed to add: {ex.Message}");
    //     }
    // }

    // public Task RemoveAppointmentFromCalendarAsync(Appointment appt)
    // {
    //     throw new NotImplementedException();
    //
    //     // var user = await _connection.Filter<Entity, User>()
    //     //     .Eq(x => x.Id, appt.EntityId)
    //     //     .FirstOrDefaultAsync();
    //
    //     // if (user==null)
    //     // {
    //     //     Logger.LogError("{userId} not found, can't delete {appointmentId}", appt.EntityId, appt.Id);
    //     //     return (false, externalId, "Can't remove from calendar");
    //     // }
    // }
}