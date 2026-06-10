using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Newtonsoft.Json;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Exceptions;

namespace Services;

// TODO: move to a different "flow app"
public class StoredProcedureService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;

    public StoredProcedureService(
        ILogger<StoredProcedureService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection
    ) : base(logger, configuration, messageBroker)
    {
        this._connection = connection;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.RunStoredProcedure));
        mapper.Register<RunStoredProcedureAction.Message>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        evt.Acknowledge();

        try
        {
            switch (evt.Body)
            {
                case RunStoredProcedureAction.Message post:
                    await ProcessAsync(post);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }
    }

    private async Task ProcessAsync(RunStoredProcedureAction.Message post)
    {
        using var scope = Logger.AddScope(new
        {
            post.Options.StoredProcedure,
            post.Event.TargetId,
            post.Event.ObjectType,
        });

        Logger.LogInformation("Process stored procedure");

        try
        {
            var evt = post.Event;
            var dict = new Dictionary<string, object>();
            if (post.Options.Parameters?.Count > 0)
            {
                foreach (var param in post.Options.Parameters)
                {
                    var value = param.Value.Contains("{{") ?
                        HandlebarsDotNet.Handlebars.Compile(param.Value).Invoke(evt) :
                        param.Value;

                    dict.TryAdd(param.Key, value);
                }
            }

            // add "default parameters" 
            dict.TryAdd(nameof(FlowEvent.AccountId), evt.AccountId.AsSerializedId());
            dict.TryAdd($"{evt.ObjectType}Id", evt.TargetId.AsSerializedId());
            dict.TryAdd("Id", evt.TargetId.AsSerializedId());
            if (evt.Refs != null)
            {
                foreach (var refValue in evt.Refs)
                {
                    dict.TryAdd(refValue.Key, refValue.Value);
                }
            }

            var id = $"{evt.AccountId:N}.{post.Options.StoredProcedure}";
            var sp = await _connection.DipperOrDefaultAsync(id);
            if (sp == null)
            {
                throw new NotFoundException($"Stored Procedure Not Found: {id}");
            }

            foreach (var p in sp.Parameters)
            {
                if (!dict.TryGetValue(p.Name, out var value) && p.DefaultValue == null)
                {
                    throw new BadRequestException($"Missing required parameter: {p.Name}");
                }

                // TODO: convert value based on parameter type
                // ...
            }

            var result = await sp.ExecuteAsync(_connection, dict);

            Logger.LogInformation("Executed: {Result}", JsonConvert.SerializeObject(result));

            await dispatchEvent(evt.Description ?? $"Executed Stored Procedure {post.Options.StoredProcedure}", true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to execute stored procedure");
            await dispatchEvent(ex.Message, false);
        }

        async Task dispatchEvent(string message, bool success = false)
        {
            if (!success) Logger.LogError(message);

            var evt = new GenericFlowEvent(post.Event)
            {
                Action = nameof(ActionIds.RunStoredProcedure),
                Description = message,
                EventTypeId = success ? post.Options.NextEventId :
                    post.Options.ErrorEventId.HasValue ? post.Options.ErrorEventId : post.Options.NextEventId,
            };

            await MessageBroker.DispatchAsync(evt, !success && !post.Options.ErrorEventId.HasValue);
        }
    }
}