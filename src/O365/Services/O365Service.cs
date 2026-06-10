using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using MongoDB.Driver;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.O365;
using PI.Shared.O365.Extensions;
using Entity = PI.Shared.Models.Entity;
using EventType = Microsoft.Graph.EventType;
using User = PI.Shared.Models.User;

namespace PI.Shared.Services;

public class O365Service
{
    private readonly IMapper _mapper;
    private readonly ILogger<O365Service> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly O365AuthClient _authClient;

    public O365Service(
        IMapper mapper,
        ILogger<O365Service> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        O365AuthClient authClient
    )
    {
        _mapper = mapper;
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _authClient = authClient;
    }

    public async Task<Result<O365IntegrationConfiguration>> GetIntegrationConfigurationAsync(IEntityContext context)
    {
        var integrations = await _connection.Filter<O365IntegrationConfiguration>()
            .Eq(x => x.AccountId, context.AccountId)
            .In(x => x.EntityId, context.GetEntityIds())
            .Eq(x => x.IntegrationId, IntegrationIds.Office365)
            .FindAsync();

        var integration = integrations.GetMoreSpecific(context);

        if (integration == null)
        {
            _logger.LogError("Integration configuration not found");
            return Result.Error<O365IntegrationConfiguration>("Integration not found");
        }

        return Result.Success(integration);
    }

    public async Task<O365Event> OnGraphEventUpdatedAsync(IEntityContext context, string graphEventId)
    {
        Event evt;
        try
        {
            evt = await GetGraphEventAsync(context, graphEventId);
        }
        catch (ServiceException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(ex, "{GraphEventId} not found, can't update", graphEventId);
            return null;
        }

        var model = await UpsertAsync(Map(context, evt));

        if (evt.Type == EventType.SeriesMaster)
        {
            // reload occurrences 
            await ReloadEventSeriesActionAsync(context, model);
        }

        _logger.LogInformation("{EventId} updated for {UserId}", model.Id, context.UserId);

        return model;
    }

    public async Task<Result<O365Event>> CreateCalendarEventAsync(IEntityContext context, Event evt, Guid? appointmentId)
    {
        using var scope = _logger.AddScope(new
        {
            AppointmentId = appointmentId,
            evt.Id,
        });

        _logger.LogInformation("Create O365 Event");

        O365Event result;
        try
        {
            evt = await AddAsync(context, evt);

            var model = _mapper.Map<O365Event>(evt);
            model.EntityId = context.UserId.Value;
            model.AccountId = context.AccountId.Value;
            model.LastModifiedOn = DateTime.UtcNow;
            model.LastActor = context.Actor();
            model.AppointmentId = appointmentId;

            result = await UpsertAsync(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to Create O365 Event");
            return Result.Error<O365Event>(ex.Message);
        }

        if (result == null) return Result.Error<O365Event>("Failed to upsert");
        if (!result.AppointmentId.HasValue) return Result.Success(result);

        _logger.LogInformation("Add Integration info to {AppointmentId}", appointmentId);

        // update appointment
        await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, result.AppointmentId.Value)
            .Update
            .Set(x => x.Refs["o365_Event"], result.Id)
            .Push(x => x.Integrations, new AppointmentIntegration
            {
                IntegrationId = IntegrationIds.Office365,
                ExternalId = result.Id.ToString(),
                Url = result.WebLink,
                Status = "Created",
                Data = new Dictionary<string, object>
                {
                    { nameof(O365Event.ExternalId), result.ExternalId },
                    { nameof(O365Event.Start), result.Start },
                    { nameof(O365Event.End), result.End },
                }
                // Status = o365Event.Value.
            })
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateOneAsync();

        // should it fire update event?
        // ...

        return Result.Success(result);
    }

    private UpdateQuery<O365Event> UpsertQuery(O365Event evt)
    {
        var update = _connection.Filter<O365Event>()
                .Eq(x => x.AccountId, evt.AccountId)
                .Eq(x => x.EntityId, evt.EntityId)
                .Eq(x => x.ExternalId, evt.ExternalId)
                .Update
                .SetOnInsert(x => x.AccountId, evt.AccountId)
                .SetOnInsert(x => x.EntityId, evt.EntityId)
                .SetOnInsert(x => x.ExternalId, evt.ExternalId)
                .SetOnInsert(x => x.Id, evt.Id)
                .SetOnInsert(x => x.CreatedOn, evt.CreatedOn)
                // always update
                .SetOrUnset(x => x.Description, evt.Description)
                .SetOrUnset(x => x.Name, evt.Name)
                .SetOrUnset(x => x.LastModifiedOn, evt.LastModifiedOn)
                .SetOrUnset(x => x.ShowAs, evt.ShowAs)
                .SetOrUnset(x => x.Type, evt.Type)
                .SetOrUnset(x => x.ResponseStatus, evt.ResponseStatus)
                .SetOrUnset(x => x.Sensitivity, evt.Sensitivity)
                .SetOrUnset(x => x.IsCancelled, evt.IsCancelled)
                .SetOrUnset(x => x.IsAllDay, evt.IsAllDay)
                .SetOrUnset(x => x.ICalUId, evt.ICalUId)
                .SetOrUnset(x => x.Categories, evt.Categories)
                .SetOrUnset(x => x.WebLink, evt.WebLink)
                .SetOrUnset(x => x.Instances, evt.Instances)
                // always
                .Set(x => x.LastActor, evt.LastActor)
                .Set(x => x.Start, evt.Start)
                .Set(x => x.End, evt.End)
            ;

        if (!string.IsNullOrEmpty(evt.MasterExternalId)) update.Set(x => x.MasterExternalId, evt.MasterExternalId);
        if (evt.SeriesMasterId.HasValue) update.Set(x => x.SeriesMasterId, evt.SeriesMasterId);
        if (evt.AppointmentId.HasValue) update.Set(x => x.AppointmentId, evt.AppointmentId);

        return update;
    }
    
    private async Task<O365Event> UpsertAsync(O365Event evt)
    {
        return await UpsertQuery(evt).UpdateAndGetOneAsync(true);
    }

    public async Task OnGraphEventDeletedAsync(IEntityContext context, string graphEventId)
    {
        var evt = await FindEventAsync(context, graphEventId);
        if (evt == null)
        {
            _logger.LogInformation("Couldn't find event to delete");
            return;
        }

        await DeleteAsync(context, evt);
    }

    public Task<O365Event> FindEventAsync(IEntityContext context, string graphEventId)
    {
        return _connection.Filter<O365Event>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.UserId.Value)
            .Eq(x => x.ExternalId, graphEventId)
            .FirstOrDefaultAsync();
    }

    public async Task CreateOrRenewSubscriptionAsync(IEntityContext context)
    {
        if (!context.UserId.HasValue)
        {
            throw new BadRequestException("Can't create subscription w/o user");
        }

        _logger.LogDebug("CreateOrRenewSubscriptionAsync {userId}", context.UserId.Value);

        var subscription = await _connection.Filter<O365Subscription>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.UserId.Value)
            .FirstOrDefaultAsync();

        if (subscription != null)
        {
            try
            {
                await RenewAsync(context, subscription);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to renew subscription {subscription}", subscription.Id);
            }
        }

        // subscribe to events
        subscription = await SubscribeToEventsAsync(context);
        _logger.LogInformation("Created subscription {subscription} for o365 User {user}", subscription.Id, context.UserId.Value);
    }

    /// <summary>
    /// Reload events from o365
    /// </summary>
    public async Task ReloadEventsAsync(IEntityContext context)
    {
        if (!context.UserId.HasValue)
        {
            throw new BadRequestException("Can't load events w/o user");
        }

        // delete all events 
        var deleted = await _connection.Filter<O365Event>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.UserId.Value)
            .Ne(x => x.Type, CalendarEventType.SeriesMaster) // do not delete master events 
            .DeleteAsync();

        _logger.LogInformation("{Count} deleted for {UserId}", deleted, context.UserId.Value);

        // initial sync
        var start = DateTime.UtcNow.AddDays(-1);
        var end = start.AddDays(180);
        var query = await GetGraphCalendarViewAsync(context, start, end);
        var count = await _connection.BulkWriteAsync(GenerateUpdates(context, query.ReadAll()));

        _logger.LogInformation("Get events for {UserId} from {Start} to {End}: {Count} updated", context.UserId, start, end, count);
    }

    /// <summary>
    /// Reload next 6 months of instances for a series
    /// </summary>
    private async Task ReloadEventSeriesActionAsync(IEntityContext context, O365Event masterEvent)
    {
        await DeleteInstancesAsync(context, masterEvent);

        // request refresh for instances
        var start = DateTime.UtcNow.AddDays(-1);
        var end = start.AddDays(180);

        _logger.LogDebug("Get Instances for {UserId}:{MasterEventId}", context.UserId.Value, masterEvent.Id);
        var query = await GetGraphInstancesAsync(context, masterEvent, start, end);
        await _connection.BulkWriteAsync(GenerateUpdates(context, query.ReadAll(), masterEvent));
    }

    private async IAsyncEnumerable<WriteModel<O365Event>> GenerateUpdates(IEntityContext context, IAsyncEnumerable<Event> events, O365Event masterEvent = null)
    {
        await foreach (var evt in events)
        {
            var model = Map(context, evt, masterEvent);
            
            // yield return _connection.Filter<O365Event>().InsertOneModel(model);
            yield return UpsertQuery(model).UpdateOneModel(true);
        }
    }

    public async IAsyncEnumerable<O365Event> GetGraphEvents(IEntityContext context, DateTime start, DateTime end)
    {
        var view = await GetGraphCalendarViewAsync(context, start, end);
        await foreach (var evt in view.ReadAll())
        {
            yield return Map(context, evt);
        }
    }

    private async Task<ICalendarCalendarViewCollectionRequest> GetGraphCalendarViewAsync(IEntityContext context, DateTime start, DateTime end)
    {
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, context.UserId.Value)
            .FirstOrDefaultAsync();

        var identity = user.FirstIdentity(ExternalProvider.Microsoft);
        if (string.IsNullOrWhiteSpace(identity?.ExternalId)) throw new NotFoundException("Couldn't find Microsoft Identity");

        var client = await _authClient.GetClientAsync(context);

        // this will get ALL events and masterseries (but no instances/exceptions)
        // var query = client
        //     .Users[identity.ExternalId]
        //     .Events
        //     .Request()
        //     ;

        // this will get all events (and instances, but no masterseries events) in the range
        var query = client
                .Users[identity.ExternalId]
                .Calendar
                .CalendarView
                .Request()
            ;

        query.QueryOptions.Add(new QueryOption("startDateTime", start.ToString("o")));
        query.QueryOptions.Add(new QueryOption("endDateTime", end.ToString("o")));

        // define the size of the page 
        query.QueryOptions.Add(new QueryOption("$top", "100"));

        return query;
    }

    public async Task<IUserEventsCollectionPage> GetEventsAsync(GraphServiceClient client, string userId)
    {
        var query = client
                .Users[userId]
                .Events
                .Request()
            ;

        var result = await query.GetAsync();

        return result;
    }

    public async Task<IUserCalendarsCollectionPage> GetCalendarsAsync(GraphServiceClient client, string userId)
    {
        var query = client
                .Users[userId]
                .Calendars
                .Request()
            ;

        var result = await query.GetAsync();

        return result;
    }

    public async Task<IGraphServiceSubscriptionsCollectionPage> GetSubscriptionsForTenantAsync(Guid tenantId)
    {
        var client = await _authClient.GetClientForTenantAsync(tenantId);
        var subscriptions = await GetGraphSubscriptionsAsync(client);
        return subscriptions;
    }

    private async Task<Subscription> RenewSubscriptionAsync(GraphServiceClient client, Guid subscriptionId)
    {
        var subscription = new Subscription
        {
            ExpirationDateTime = DateTime.UtcNow.AddMinutes(4200) // max is 3 days (4230 minutes)
        };

        var result = await client.Subscriptions[subscriptionId.ToString()]
            .Request()
            .UpdateAsync(subscription);

        return result;
    }

    // https://learn.microsoft.com/en-us/graph/api/message-get?view=graph-rest-1.0&tabs=csharp
    public async Task<Message> GetMessageAsync(IEntityContext context, string messageId)
    {
        var session = await GetAsync(context);

        var query = session.Client
                .Users[session.Identity.ExternalId]
                .Messages[messageId]
                .Request()
            // .Select("start,end,subject,iCalUId,subject,isAllDay,isCancelled,showAs,type,id,seriesMasterId,recurrence,instances")
            ;

        query.Headers.Add(new HeaderOption("Prefer", "outlook.body-content-type=\"text\""));

        var result = await query.GetAsync();

        return result;
    }

    public async Task<MailFolder> GetMessageFolderAsync(IEntityContext context, string folderId)
    {
        var session = await GetAsync(context);

        var query = session.Client
                .Users[session.Identity.ExternalId]
                .MailFolders[folderId]
                .Request()
            // .Select("start,end,subject,iCalUId,subject,isAllDay,isCancelled,showAs,type,id,seriesMasterId,recurrence,instances")
            ;

        var result = await query.GetAsync();

        return result;
    }

    public async Task<Microsoft.Graph.User> GetUserAsync(IEntityContext context)
    {
        var session = await GetAsync(context);

        var query = session.Client
                .Users[session.Identity.ExternalId]
                .Request()
                .Select("displayName,otherMails,mail")
            ;

        var result = await query.GetAsync();

        return result;
    }

    public async Task<Event> GetGraphEventAsync(IEntityContext context, string graphEventId)
    {
        var session = await GetAsync(context);

        var query = session.Client
                .Users[session.Identity.ExternalId]
                .Events[graphEventId]
                .Request()
            // .Select("start,end,subject,iCalUId,subject,isAllDay,isCancelled,showAs,type,id,seriesMasterId,recurrence,instances")
            ;

        var result = await query.GetAsync();

        return result;
    }

    public async Task<Event> GetGraphEventAsync(GraphServiceClient client, string userId, string eventId)
    {
        var query = client
                .Users[userId]
                .Events[eventId]
                .Request()
            // .Select("start,end,subject,iCalUId,subject,isAllDay,isCancelled,showAs,type,id,seriesMasterId,recurrence,instances")
            ;

        var result = await query.GetAsync();

        return result;
    }

    private async Task<IEventInstancesCollectionRequest> GetGraphInstancesAsync(IEntityContext context, O365Event masterEvent, DateTime? startDate = null, DateTime? endDate = null)
    {
        var session = await GetAsync(context);

        var query = session.Client
                .Users[session.Identity.ExternalId]
                .Events[masterEvent.ExternalId]
                .Instances
                .Request()
            ;

        startDate ??= DateTime.UtcNow;
        endDate ??= startDate.Value.AddDays(31);

        query.QueryOptions.Add(new QueryOption("startDateTime", startDate.Value.ToString("o")));
        query.QueryOptions.Add(new QueryOption("endDateTime", endDate.Value.ToString("o")));

        // define the size of the page 
        query.QueryOptions.Add(new QueryOption("$top", "100"));

        return query;
    }

    private async Task RemoveGraphSubscriptionAsync(GraphServiceClient client, string subscriptionId)
    {
        await client.Subscriptions[subscriptionId].Request().DeleteAsync();
    }

    private async Task<Subscription> GetGraphSubscriptionAsync(GraphServiceClient client, string subscriptionId)
    {
        var result = await client.Subscriptions[subscriptionId].Request().GetAsync();
        return result;
    }

    private async Task<IGraphServiceSubscriptionsCollectionPage> GetGraphSubscriptionsAsync(GraphServiceClient client)
    {
        var result = await client.Subscriptions.Request().GetAsync();
        return result;
    }

    private async Task DeleteAsync(IEntityContext context, O365Event evt)
    {
        if (evt.Type == CalendarEventType.SeriesMaster)
        {
            // explicitly delete instances because cascading doesn't work in this case
            await DeleteInstancesAsync(context, evt);
        }

        await _connection.Filter<O365Event>()
            .Eq(x => x.Id, evt.Id)
            .DeleteOneAsync();

        if (!evt.AppointmentId.HasValue) return;

        _logger.LogInformation("Deleting event associated with {AppointmentId}", evt.AppointmentId);

        var now = DateTime.UtcNow;
        // TODO: should automatically cancel appointment?
        // ...
        var appt = await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.Id, evt.AppointmentId.Value)
            .Ne(x => x.IsActive, false)
            .Eq(x => x.CancelledOn, null)
            .ElemMatchBuilder(
                x => x.Integrations,
                q => q
                    .Eq(x => x.IntegrationId, IntegrationIds.Office365)
                    .Eq(x => x.ExternalId, evt.Id.ToString())
            )
            .Update
            .Set(x => x.CancelledOn, now)
            .Set(x => x.CancelledBy, evt.EntityId)
            .Set($"{nameof(Appointment.Integrations)}.$.{nameof(AppointmentIntegration.Status)}", "Cancelled")
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        if (appt != null)
        {
            await _objectTypeService.FireObjectUpdatedAsync(
                context,
                appt,
                new Dictionary<string, object>
                {
                    { nameof(Appointment.CancelledOn), now },
                    { nameof(Appointment.CancelledBy), evt.EntityId },
                },
                e => { }
            );
        }
    }

    private async Task DeleteInstancesAsync(IEntityContext context, O365Event masterEvent)
    {
        if (masterEvent.Type != CalendarEventType.SeriesMaster)
        {
            // ...
            return;
        }

        _logger.LogDebug("Delete Instances of {eventId}", masterEvent.Id);

        await _connection.Filter<O365Event>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.UserId.Value)
            .Eq(x => x.SeriesMasterId, masterEvent.Id)
            .DeleteAsync();

        await _connection.Filter<O365Event>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.UserId.Value)
            .Eq(x => x.Id, masterEvent.Id)
            .Update
            .Unset(x => x.Instances)
            .UpdateOneAsync();
    }

    private O365Event Map(IEntityContext context, Event evt, O365Event masterEvent = null)
    {
        var model = _mapper.Map<O365Event>(evt);
        model.EntityId = context.UserId.Value;
        model.AccountId = context.AccountId.Value;
        model.LastModifiedOn = DateTime.UtcNow;
        model.LastActor = context.Actor();
        model.SeriesMasterId = masterEvent?.Id;

        return model;
    }

    public async Task<O365Subscription> RenewAsync(IEntityContext context, O365Subscription subscription)
    {
        var client = await _authClient.GetClientAsync(context);
        var graph = await RenewSubscriptionAsync(client, subscription.Id);

        await _connection.Filter<O365Subscription>()
            .Eq(x => x.Id, subscription.Id)
            .Update
            .Set(x => x.ExpiresOn, graph.ExpirationDateTime.Value.UtcDateTime)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateOneAsync();

        return subscription;
    }

    public async Task<O365Subscription> UnsubscribeToEventsAsync(IEntityContext context, O365Subscription subscription)
    {
        var session = await GetAsync(context);
        await RemoveGraphSubscriptionAsync(session.Client, subscription.Id.ToString());

        await _connection.Filter<O365Subscription>()
            .Eq(x => x.Id, subscription.Id)
            .DeleteOneAsync();

        return subscription;
    }

    public async Task<O365Subscription> SubscribeToMessagesAsync(IEntityContext context)
    {
        // /users/{id}/messages
        if (!context.UserId.HasValue) throw new BadRequestException("Missing user");

        var session = await GetAsync(context);

        return await SubscribeAsync(context, session.Client, $"users/{session.Identity.ExternalId}/messages", new[] { "created", "updated" });
    }

    /// <summary>
    /// Subscribe to all calendar event changes
    /// </summary>
    public async Task<O365Subscription> SubscribeToEventsAsync(IEntityContext context)
    {
        if (!context.UserId.HasValue) throw new BadRequestException("Missing user");

        var session = await GetAsync(context);

        return await SubscribeAsync(context, session.Client, $"users/{session.Identity.ExternalId}/events", new[] { "created", "updated", "deleted" }, "SomeSecret");
    }

    /// <summary>
    /// Create webhook subscription
    /// </summary>
    private async Task<O365Subscription> SubscribeAsync(IEntityContext context, GraphServiceClient client, string resource, string[] changeType, string clientState = null)
    {
        // https://developer.microsoft.com/en-us/graph/docs/api-reference/v1.0/resources/webhooks
        var subscription = new Subscription
        {
            ChangeType = string.Join(',', changeType),
            Resource = resource,
            NotificationUrl = $"{_authClient.Config.NotificationControllerUrl}/{context.AccountId}/{context.UserId}",
            ClientState = clientState ?? Guid.NewGuid().ToString(),
            ExpirationDateTime = DateTime.UtcNow.AddMinutes(4200) // max is 3 days (4230 minutes)
        };

        var result = await client.Subscriptions
            .Request()
            .AddAsync(subscription);

        var dao = new O365Subscription
        {
            Id = Guid.Parse(result.Id),
            AccountId = context.AccountId.Value,
            EntityId = context.UserId.Value,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            LastModifiedOn = DateTime.UtcNow,

            ExpiresOn = result.ExpirationDateTime.Value.UtcDateTime,
            Resource = result.Resource,
            NotificationUrl = result.NotificationUrl,
        };

        await _connection.InsertAsync(dao);

        return dao;
    }

    public async Task<Message> AddAsync(Account account, string userId, Message message)
    {
        var client = _authClient.GetClient(account);
        if (client == null) throw new BadRequestException("Failed to get o365 token");

        var request = client
                .Users[userId]
                .Messages
                .Request()
            ;

        var result = await request.AddAsync(message);

        return result;
    }

    private async Task<Event> AddAsync(IEntityContext context, Event evt)
    {
        var session = await GetAsync(context);

        var request = session
                .Client
                .Users[session.Identity.ExternalId]
                .Events
                .Request()
            ;

        var result = await request.AddAsync(evt);

        return result;
    }

    public async Task<Result<O365Event>> DeleteEventFromCalendarAsync(IEntityContext context, O365Event evt)
    {
        try
        {
            await RemoveEventFromCalendarAsync(context, evt);
        }
        catch (Microsoft.Graph.ServiceException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(ex, "{GraphEventId} not found, can't delete", evt.ExternalId);
        }
        catch (Exception ex)
        {
            return Result.Error<O365Event>(ex.Message);
        }

        await DeleteAsync(context, evt);

        return Result.Success(evt);
    }

    private async Task<O365Event> RemoveEventFromCalendarAsync(IEntityContext context, O365Event evt)
    {
        var session = await GetAsync(context);

        _logger.LogDebug("Delete Event {EventId} for {UserId}", evt.Id, context.UserId);

        await session
            .Client
            .Users[session.Identity.ExternalId]
            .Events[evt.ExternalId]
            .Request()
            .DeleteAsync();

        return evt;
    }

    private async Task<(EntityIdentity Identity, GraphServiceClient Client)> GetAsync(IEntityContext context)
    {
        if (!context.UserId.HasValue) throw new BadRequestException("Missing User");

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, context.UserId.Value)
            .FirstOrDefaultAsync();

        if (user == null) throw NotFoundException.New<User>(context.UserId.Value);

        var identity = user.FirstIdentity(ExternalProvider.Microsoft);
        if (identity == null) throw new NotFoundException("Microsoft Identity not found");

        var client = await _authClient.GetClientAsync(context);
        if (client == null) throw new BadRequestException("Failed to get o365 token");

        return (identity, client);
    }
}