using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using HandlebarsDotNet;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Models.Http;

namespace Zapier.Services;

/// <summary>
/// Executes the <c>HttpCallOut</c> flow action: a templated (Handlebars) outbound HTTP
/// request fired as a flow step. This is a generic flow-action runner — unrelated to
/// Zapier REST Hook subscriptions, which are delivered by
/// <c>PI.Shared.Integrations.Delivery.WebhookEventListener</c> and its pipeline.
/// </summary>
public class WebhookService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhookService(
        ILogger<WebhookService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        MongoConnection connection,
        IHttpClientFactory httpClientFactory)
        : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _httpClientFactory = httpClientFactory;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.HttpCallOut));
        mapper.Register<SimpleActionMessage<HttpCallOutActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage message)
    {
        try
        {
            if (message.Body is SimpleActionMessage<HttpCallOutActionOptions> http)
            {
                await HttpCallOutAsync(http);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", message.RoutingKey);
        }

        message.Acknowledge();
    }

    private async Task HttpCallOutAsync(SimpleActionMessage<HttpCallOutActionOptions> action)
    {
        using var scope = Logger.AddScope(new
        {
            action.Event.ObjectType,
            action.Event.TargetId,
            action.Options.Url,
            action.Options.Method,
        });

        Logger.LogInformation("Make Http request");

        try
        {
            var callOut = await ExecuteHttpCallOutAsync(action);
            var message = $"{callOut.Request.Method} {callOut.Request.Url} returned {callOut.Response?.StatusCode}";
            var evt = new GenericFlowEvent(action.Event)
            {
                Action = nameof(ActionIds.HttpCallOut),
                Description = (callOut.Response?.Succeeded ?? false) ?
                    action.GetEventDescription(action.Options.NextEventId, message) :
                    message,
                EventTypeId = action.Options?.NextEventId,
            };

            evt.AddRefValue(nameof(HttpCallOut), callOut.Id);

            evt.SetMetaValue("Response|StatusCode", callOut.Response?.StatusCode ?? 0);
            evt.SetMetaValue("Response|Succeeded", callOut.Response?.Succeeded ?? false);

            if (callOut.Response?.Headers?.TryGetValue("Content-Type", out var contentType) ?? false)
            {
                if ((contentType?.FirstOrDefault()?.StartsWith("application/json") ?? false) && !string.IsNullOrWhiteSpace(callOut.Response.Body))
                {
                    var response = JsonConvert.DeserializeObject<ExpandoObject>(callOut.Response.Body).FlattenAllProperties("JsonResponse");
                    foreach (var kvp in response)
                    {
                        evt.SetMetaValue(kvp.Key, kvp.Value);
                    }
                }
            }

            await MessageBroker.DispatchAsync(evt);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to execute request");
        }
    }

    private async Task<HttpCallOut> ExecuteHttpCallOutAsync(SimpleActionMessage<HttpCallOutActionOptions> action)
    {
        var templateContext = await BuildHandlebarsContext(action.Event);
        var url = render(action.Options.Url);
        var body = render(action.Options.Body);
        var headers = new Dictionary<string, string[]>(
            (action.Options.Headers ?? Enumerable.Empty<KeyValuePair<string, string>>())
            .Select(x => new KeyValuePair<string, string[]>(x.Key, new[] { render(x.Value) }))
        );

        if (!string.IsNullOrEmpty(body))
        {
            headers["Content-Length"] = new[] { Encoding.UTF8.GetBytes(body).Length.ToString() };
        }

        var callout = new HttpCallOut
        {
            Id = Guid.NewGuid(),
            AccountId = action.Event.AccountId,
            CreatedOn = DateTime.UtcNow,
            Request = new Request
            {
                Method = Method.Post,
                Url = url,
                Headers = headers,
                Body = body,
            },
            Refs = (action.Event.Refs ?? Enumerable.Empty<KeyValuePair<string, object>>())
                .Append(new KeyValuePair<string, object>(action.Event.ObjectType, action.Event.TargetId))
                .Distinct(new KeyValueComparer())
                .ToList()
        };

        await _connection.InsertAsync(callout);

        return await SendAsync(callout);

        string render(string template)
        {
            if (string.IsNullOrWhiteSpace(template)) return template;
            return Handlebars.Compile(template).Invoke(templateContext);
        }
    }

    private async Task<ExpandoObject> BuildHandlebarsContext(FlowEvent evt)
    {
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.Id, evt.RunId)
            .FirstOrDefaultAsync();

        return flowRun.BuildHandlebarsContext(evt);
    }

    private class KeyValueComparer : IEqualityComparer<KeyValuePair<string, object>>
    {
        public bool Equals(KeyValuePair<string, object> x, KeyValuePair<string, object> y)
        {
            return x.Key == y.Key && Equals(x.Value, y.Value);
        }

        public int GetHashCode(KeyValuePair<string, object> obj)
        {
            return HashCode.Combine(obj.Key, obj.Value);
        }
    }

    private async Task<HttpCallOut> SendAsync(HttpCallOut callout)
    {
        var prepare = await _connection.Filter<HttpCallOut>()
            .Eq(x => x.Id, callout.Id)
            .Eq(x => x.Response, null)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.RetryAfter, DateTime.UtcNow.AddMinutes(10))
            .UpdateAndGetOneAsync();

        if (prepare == null)
        {
            Logger.LogInformation("Nothing else to do with this {CallOutId}", callout.Id);
            return null;
        }

        callout = prepare;

        var client = _httpClientFactory.CreateClient("Webhook");
        try
        {
            var response = await client.SendAsync(callout);
            if (response.IsSuccess)
            {
                callout = await _connection.Filter<HttpCallOut>()
                    .Eq(x => x.Id, callout.Id)
                    .Update
                    .Set(x => x.Response, response)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Unset(x => x.RetryAfter)
                    .UpdateAndGetOneAsync();
            }
            else
            {
                var query = _connection.Filter<HttpCallOut>()
                        .Eq(x => x.Id, callout.Id)
                        .Update
                        .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    ;

                if (callout.FailedAttempts?.Length > 3)
                {
                    query.Set(x => x.Response, response)
                        .Unset(x => x.RetryAfter);
                }
                else
                {
                    query.Push(x => x.FailedAttempts, response)
                        .Set(x => x.RetryAfter, DateTime.UtcNow + callout.FailedAttempts?.Length switch
                        {
                            1 => TimeSpan.FromMinutes(5),
                            2 => TimeSpan.FromMinutes(10),
                            3 => TimeSpan.FromMinutes(30),
                            _ => TimeSpan.FromMinutes(1),
                        });
                }

                callout = await query.UpdateAndGetOneAsync();
            }

            return callout;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send request");
            throw;
        }
    }
}
