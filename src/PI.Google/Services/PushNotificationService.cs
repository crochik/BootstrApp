using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Messaging;
using Crochik.Mongo;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Google.Models;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Notifications;
using PI.Shared.Services;
using Notification = FirebaseAdmin.Messaging.Notification;

namespace PI.Google.Services;

public class PushNotificationService : AbstractMessageQueueService, ILifetimeService
{
    private readonly IConfiguration _configuration;
    private readonly ObjectTypeService _objectTypeService;
    private readonly MongoConnection _connection;
    private Dictionary<string, FirebaseApp> _cachedApps = new();
    private readonly string _baseUrl;

    public PushNotificationService(ILogger<PushNotificationService> logger, IConfiguration configuration, IMessageBroker messageBroker, ObjectTypeService objectTypeService, MongoConnection connection) :
        base(logger, configuration, messageBroker)
    {
        _configuration = configuration;
        _objectTypeService = objectTypeService;
        _connection = connection;
        _baseUrl = configuration.GetValue<string>("BaseUrl");
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.SendNotification));
        mapper.Register<SimpleActionMessage<SendNotificationActionOptions>>();
    }

    private FirebaseMessaging GetFirebaseMessaging(string clientId = null)
    {
        clientId ??= "PI";

        if (!_cachedApps.TryGetValue(clientId, out var app))
        {
            var configFile = _configuration.GetSection(nameof(PushNotificationService))[clientId];
            if (string.IsNullOrEmpty(configFile))
            {
                Logger.LogError("Didn't find configuration for {ClientId}", clientId);
                throw new NotFoundException($"Didn't find Configuration for Client: {clientId}");
            }

            app = FirebaseApp.Create(
                new AppOptions
                {
                    Credential = GoogleCredential.FromJson(configFile)
                },
                clientId
            );
            
            _cachedApps.TryAdd(clientId, app);
        }

        return FirebaseMessaging.GetMessaging(app);
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case SimpleActionMessage<SendNotificationActionOptions> notification:
                    await ProcessAsync(notification);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {RoutingKey}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }

    private async Task ProcessAsync(SimpleActionMessage<SendNotificationActionOptions> action)
    {
        Result<Shared.Models.Notifications.Notification> result;
        try
        {
            result = await SendAsync(action);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send");
            result = Result.Error<Shared.Models.Notifications.Notification>(ex.Message);
        }

        if (result.IsUnknown) return;

        var evt = new GenericFlowEvent(action.Event)
        {
            Action = nameof(ActionIds.SendNotification),
            Description = result.Status,
            EventTypeId = action.Options.PushNotificationEventId,
        };

        if (result.IsSuccess)
        {
            evt.AddRefValue(nameof(Shared.Models.Notifications.Notification), result.Value.Id);
        }

        await MessageBroker.DispatchAsync(evt, result.IsError);
    }

    private async Task<Result<Shared.Models.Notifications.Notification>> SendAsync(SimpleActionMessage<SendNotificationActionOptions> action)
    {
        var accountContext = new AccountContext(action.Event.AccountId);

        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.RunId)
            .FirstOrDefaultAsync();

        var flowContext = flowRun.BuildHandlebarsContext(action.Event);
        if (!ExpressionEvaluatorService.TryResolve(accountContext, flowContext, action.Options.EntityId, out var entityObj) ||
            (entityObj is not Guid entityId && (entityObj is not string entityIdStr || !Guid.TryParse(entityIdStr, out entityId)))
           )
        {
            Logger.LogError("Fail to resolve EntityId");
            return Result.Error<Shared.Models.Notifications.Notification>("Couldn't resolve EntityId");
        }

        if (!ExpressionEvaluatorService.TryResolve(accountContext, flowContext, action.Options.Title, out var titleObj) || titleObj is not string title)
        {
            Logger.LogError("Fail to resolve Title");
            return Result.Error<Shared.Models.Notifications.Notification>("Couldn't resolve Title");
        }

        if (!ExpressionEvaluatorService.TryResolve(accountContext, flowContext, action.Options.Action, out var actionObj) || actionObj is not string actionUrl)
        {
            Logger.LogError("Fail to resolve Action");
            return Result.Error<Shared.Models.Notifications.Notification>("Couldn't resolve Action");
        }

        if (!ExpressionEvaluatorService.TryResolve(accountContext, flowContext, action.Options.Url, out var urlObj) || urlObj is not string url)
        {
            Logger.LogError("Fail to resolve Url");
            return Result.Error<Shared.Models.Notifications.Notification>("Couldn't resolve Url");
        }

        var clientId = action.Options.ClientId;
        if (!ExpressionEvaluatorService.TryResolve(accountContext, flowContext, action.Options.ClientId, out var clientIdObj))
        {
            Logger.LogError("Fail to resolve {ClientId}", clientId);
            return Result.Error<Shared.Models.Notifications.Notification>("Couldn't resolve ClientId");
        }

        clientId = clientIdObj?.ToString();
        
        var body = HandlebarsDotNet.Handlebars.Compile(action.Options.Message).Invoke(flowContext);

        var notification = new PI.Shared.Models.Notifications.Notification
        {
            AccountId = action.Event.AccountId,
            EntityId = entityId,
            CreatedOn = DateTime.UtcNow,
            Name = title,
            Description = body,
            ClientId = clientId,
            // LastActor =
            // LastModifiedOn =
            // FlowId = action.Options.FlowId,
            // ObjectStatusId = action.Options.ObjectStatusId,
            // ExpiresOn =
            IsActive = true,
            Category = action.Options.Category,
            Url = url,
            Action = actionUrl,
            Refs = new List<KeyValuePair<string, object>>
            {
                new(nameof(Entity), entityId),
                new(action.Event.ObjectType, action.Event.TargetId),
            },
        };

        var subscribers = await _connection.Filter<NotificationSubscriber>()
            .Eq(x => x.AccountId, accountContext.AccountId)
            .Eq(x => x.Category, action.Options.Category)
            .In(x => x.DestinationEntityId, new[] { default(Guid?), entityId })
            .FindAsync();

        if (subscribers.All(x => x.DestinationEntityId != entityId))
        {
            Logger.LogInformation("No subscribers for {Category} in {EntityId}", action.Options.Category, entityId);

            var evt = new GenericFlowEvent(action.Event)
            {
                Description = notification.Description,
                EventTypeId = action.Options.NoSubscriptionsEventId,
            };

            evt.AddRefValue(notification);
            evt.SetMetaValue(nameof(Notification), notification.Name);
            evt.SetMetaValue($"{nameof(Notification)}|_id", notification.Id.ToString());
            await MessageBroker.DispatchAsync(evt);
        }

        // limit to active users 
        var userIds = subscribers
            .Select(x => x.EntityId)
            .Append(entityId)
            .Distinct()
            .ToArray();

        var users = (await _connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, accountContext.AccountId)
                .In(x => x.Id, userIds)
                .Ne(x => x.IsActive, false)
                .IncludeField(x => x.Id)
                .IncludeField(x => x.Name)
                .IncludeField(x => x.Email)
                .IncludeField(x => x.Phone)
                .IncludeField("_t")
                .FindAsync()
            ).ToDictionary(x => x.Id);

        userIds = users.Keys.ToArray();

        // track 
        notification.Refs.AddRange(userIds.Select(x => new KeyValuePair<string, object>(nameof(User), x)));
        notification.Subscribers = subscribers
            .Where(x => users.ContainsKey(x.EntityId))
            .Select(x => new NotificationSubscriber
            {
                Id = x.Id,
                CommunicationChannel = x.CommunicationChannel,
                ChannelAddress = x.ChannelAddress,
                EntityId = x.EntityId,
            }).ToArray();

        notification = await _objectTypeService.InsertAsync(accountContext, notification, e =>
        {
            e.Description ??= $"Notification Created";
            e.Action ??= "ObjectCreated";
            notification.Refs.ForEach(x => e.AddRefValue(x.Key, x.Value));
        });

        // fire events for other channels
        foreach (var subscriber in subscribers)
        {
            if (!users.TryGetValue(subscriber.EntityId, out var user)) continue;

            switch (subscriber.CommunicationChannel)
            {
                case CommunicationChannel.Email:
                {
                    Logger.LogInformation("Send Email to {User}: {EmailAddress}", user.Name, subscriber.ChannelAddress ?? user.Email);

                    var evt = new GenericFlowEvent(action.Event)
                    {
                        Description = notification.Description,
                        EventTypeId = action.Options.EmailNotificationEventId,
                    };

                    evt.AddRefValue(user);
                    evt.AddRefValue(notification);
                    evt.SetMetaValue(nameof(Notification), notification.Name);
                    evt.SetMetaValue($"{nameof(Notification)}|_id", notification.Id.ToString());
                    evt.SetMetaValue($"{nameof(User)}|_id", user.Id.ToString());
                    evt.SetMetaValue($"{nameof(User)}|{nameof(User.Name)}", user.Name);

                    evt.SetMetaValue($"{nameof(User)}|{nameof(User.Email)}", subscriber.ChannelAddress ?? user.Email);
                    await MessageBroker.DispatchAsync(evt);
                    break;
                }

                case CommunicationChannel.SMS:
                {
                    Logger.LogInformation("Send SMS to {User}: {PhoneNumber}", user.Name, subscriber.ChannelAddress ?? user.Phone);

                    var evt = new GenericFlowEvent(action.Event)
                    {
                        Description = notification.Description,
                        EventTypeId = action.Options.SMSNotificationEventId,
                    };

                    evt.AddRefValue(user);
                    evt.AddRefValue(notification);
                    evt.SetMetaValue(nameof(Notification), notification.Name);
                    evt.SetMetaValue($"{nameof(Notification)}|_id", notification.Id.ToString());
                    evt.SetMetaValue($"{nameof(User)}|_id", user.Id.ToString());
                    evt.SetMetaValue($"{nameof(User)}|{nameof(User.Name)}", user.Name);

                    evt.SetMetaValue($"{nameof(User)}|{nameof(User.Phone)}", subscriber.ChannelAddress ?? user.Phone);
                    await MessageBroker.DispatchAsync(evt);
                    break;
                }
            }
        }

        return await PushNotificationAsync(action.Event, notification);
    }

    /// <summary>
    /// Send push notification to users 
    /// </summary>
    private async Task<Result<Shared.Models.Notifications.Notification>> PushNotificationAsync(FlowEvent trigger, Shared.Models.Notifications.Notification notification)
    {
        var userIds = notification.Subscribers
            .Where(x => x.CommunicationChannel == CommunicationChannel.PushNotification)
            .Select(x => x.EntityId)
            .ToArray();

        // get registrations
        var registrations = await _connection.Filter<CloudMessageRegistration>()
            .Eq(x => x.AccountId, trigger.AccountId)
            .In(x => x.EntityId, userIds)
            .Eq(x => x.IsActive, true)
            // .Eq(x=>x.ClientId, "")
            .IncludeField(x => x.Token)
            .FindAsync();

        if (registrations.IsEmpty())
        {
            Logger.LogInformation("No active registrations found");
            return Result.Success(notification, "No active registrations found");
        }

        var fcmMessage = new MulticastMessage
        {
            Notification = new Notification()
            {
                Title = notification.Name,
                Body = notification.Description,
                // ImageUrl = 
            },
            Data = new Dictionary<string, string>
            {
                { "id", notification.Id.ToString() },
                { "objectType", trigger.ObjectType },
                { "objectId", trigger.TargetId.ToString() },
                { "category", notification.Category },
                { "backgroundUrl", $"{_baseUrl}/api/v1/Notification/{notification.Id}" },
                { "url", notification.Url },
                { "action", notification.Action }
            },
            Tokens = registrations.Select(x => x.Token).ToList(),
            // Webpush = new WebpushConfig
            // {
            //     FcmOptions = new WebpushFcmOptions
            //     {
            //         Link = $"{_baseUrl}/api/v1/Notification/{notification.Id}",
            //     }
            // },
            // Android
            // APN
        };

        var client = GetFirebaseMessaging(notification.ClientId);
        var response = await client.SendMulticastAsync(fcmMessage);

        Logger.LogInformation("Message sent: {SuccessCount} {FailureCount}", response.SuccessCount, response.FailureCount);

        var status = response.SuccessCount > 0 ? "No devices received notification" : $"Message sent to {response.SuccessCount} devices";

        if (notification.FlowId.HasValue)
        {
            var evt = new GenericFlowEvent(notification)
            {
                Action = nameof(ActionIds.SendNotification),
                Description = status,
                EventTypeId = response.SuccessCount > 0 ? EventIds.OnNotificationSent : EventIds.OnNotificationFailed,
            };

            evt.AddRefValues(notification.Refs);
            evt.TryAddMetaValue("SuccessCount", response.SuccessCount);
            evt.TryAddMetaValue("FailureCount", response.FailureCount);

            await MessageBroker.DispatchAsync(evt);
        }

        return response.SuccessCount > 0 ? Result.Success(notification, status) : Result.Error<Shared.Models.Notifications.Notification>(status);
    }

    public async Task SendAsync(string token, string title, string message, string clientId = null)
    {
        title ??= "Test";

        var msg = new Message()
        {
            Notification = new Notification()
            {
                Title = title,
                Body = message,
            },
            Data = new Dictionary<string, string>()
            {
                { "title", title },
                { "message", message },
            },
            Token = token,
        };

        var client = GetFirebaseMessaging(clientId);
        var response = await client.SendAsync(msg);

        Logger.LogInformation("Successfully sent message: {MessageId}", response);
    }
}