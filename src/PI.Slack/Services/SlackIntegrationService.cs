using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.Slack;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace Services;

public class SlackIntegrationService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly IEntityIntegrationAdapter _entityIntegrationAdapter;
    private readonly SlackClient _client;

    public SlackIntegrationService(
        ILogger<SlackIntegrationService> logger,
        MongoConnection connection,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        IEntityIntegrationAdapter entityIntegrationAdapter,
        SlackClient slackClient
    ) : base(logger, configuration, messageBroker)
    {
        this._connection = connection;
        this._entityIntegrationAdapter = entityIntegrationAdapter;
        this._client = slackClient;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.PostToSlackChannel));
        mapper.Register<PostToSlackAction.Message>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case PostToSlackAction.Message post:
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

    public async Task PostAsync(PostToSlackAction.Message post)
    {
        using var scope = Logger.AddScope(new
        {
            post.Event.ObjectType,
            ObjectId = post.Event.TargetId,
            post.Event.RunId,
        });
            
        Logger.LogInformation("Post message to slack");
            
        var url = default(string);
        switch (post.Options.To)
        {
            // case PostToSlackActionOptions.Tos.AssignedUser:
            case PostToSlackActionOptions.Tos.Account:
            {
                var list = await _entityIntegrationAdapter.GetTrunkByIdAsync(post.Event.AccountId, IntegrationIds.Slack);
                var ordered = list.OrderBy(x => x.Level).ToArray();
                url = ordered.FirstOrDefault()?.GetData<SlackIntegration.Data>()?.HookUrl;
            }
                break;

            default:
                url = post.Options.Url;
                break;
        }

        if (string.IsNullOrEmpty(url))
        {
            // fire error event
            Logger.LogError("Could not resolve Slack Url: {To}", post.Options.To);
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
            var body = HandlebarsDotNet.Handlebars.Compile(post.Options.Message).Invoke(context);

            var message = new SlackMessage
            {
                Text = body,
            };

            var result = await _client.SendMessageAsync(url, message);
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
                Action = nameof(ActionIds.PostToSlackChannel),
                Description = message,
                EventTypeId = output?.EventId,
            };
            
            await MessageBroker.DispatchAsync(evt, error);
        }
    }
}

[Obsolete]
public class HandlebarsContext
{
    public Lead Lead { get; set; }
    public Appointment Appointment { get; set; }
    public string SchedulerUrl { get; set; }
}