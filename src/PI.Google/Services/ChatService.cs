using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Google.Apis.HangoutsChat.v1.Data;
using HandlebarsDotNet;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace PI.Google.Services;

public class ChatService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly IEntityIntegrationAdapter _entityIntegrationAdapter;
    private readonly GoogleClient _client;

    public ChatService(
        ILogger<ChatService> logger,
        MongoConnection connection,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        IEntityIntegrationAdapter entityIntegrationAdapter,
        GoogleClient client
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _entityIntegrationAdapter = entityIntegrationAdapter;
        _client = client;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.PostToGoogleChat));
        mapper.Register<PostToGoogleChatAction.Message>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case PostToGoogleChatAction.Message post:
                    await PostAsync(post);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }

    private async Task PostAsync(PostToGoogleChatAction.Message post)
    {
        using var scope = Logger.AddScope(new
        {
            post.Event.ObjectType,
            ObjectId = post.Event.TargetId,
            post.Event.RunId,
        });

        Logger.LogInformation("Post message to gchat");

        var spaceId = default(string);
        var token = default(string);
        var key = default(string);

        switch (post.Options.To)
        {
            // case PostToSlackActionOptions.Tos.AssignedUser:
            case PostToGoogleChatActionOptions.Tos.Account:
            {
                var list = await _entityIntegrationAdapter.GetTrunkByIdAsync(post.Event.AccountId, IntegrationIds.Google);
                var top = list.MinBy(x => x.Level);
                if (top != null)
                {
                    var data = top.GetData<GoogleIntegration.Data>();
                    spaceId = data.ChatSpaceId;
                    key = data.ChatSpaceKey;

                    var auth = top.GetAuthentication<GoogleIntegration.Auth>();
                    token = auth.ChatSpaceToken;
                }
            }
                break;

            default:
            {
                var url = new Uri(post.Options.Url);
                if (url.Host != "chat.googleapis.com") throw new BadRequestException("Invalid host");
                var pathParts = url.AbsolutePath.Split("/", StringSplitOptions.RemoveEmptyEntries);
                if (pathParts.Length != 4 || pathParts[0] != "v1" || pathParts[1] != "spaces" || pathParts[3] != "messages") throw new BadRequestException("Invalid path");
                spaceId = pathParts[2];
                if (!url.Query.StartsWith("?")) throw new BadRequestException("Missing query");
                var query = HttpUtility.ParseQueryString(url.Query);
                key = query.Get("key");
                token = query.Get("token");
            }
                break;
        }

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(spaceId))
        {
            // fire error event
            Logger.LogError("Could not parse url: {To}", post.Options.To);
            return;
        }

        // limit to properties used
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, post.Event.AccountId)
            .Eq(x => x.Id, post.Event.RunId)
            .IncludeFields(
                x => x.Objects,
                x => x.ObjectType,
                x => x.InitialEvent,
                x => x.InitialObject
            )
            .FirstOrDefaultAsync();
        
        try
        {
            var context = flowRun.BuildHandlebarsContext(post.Event);
            var body = Handlebars.Compile(post.Options.Message).Invoke(context);
            var message = new Message
            {
                Text = body,
            };

            var result = await _client.SendAsync(spaceId, message, key, token);
            await fireAsync(result.IsError, result.IsError ? result.Status : "Message posted");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to post message: {Message}", post.Options.Message);
            await fireAsync(true, ex.Message);
        }

        async Task fireAsync(bool error, string message)
        {
            // fire event
            var output = post.Options.Output?.FirstOrDefault();
            var evt = new GenericFlowEvent(post.Event)
            {
                Action = nameof(ActionIds.PostToGoogleChat),
                Description = message,
                EventTypeId = output?.EventId,
            };
            
            await MessageBroker.DispatchAsync(evt, error);
        }
    }
}