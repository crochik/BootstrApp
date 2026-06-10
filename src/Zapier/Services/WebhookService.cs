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
using PI.Shared.Services;
using Zapier.Models;

namespace Zapier.Services;

public class WebhookService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhookService(
        ILogger<WebhookService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        IHttpClientFactory httpClientFactory)
        : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
        _httpClientFactory = httpClientFactory;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, FlowObjectEventRoute.Create.GetRoute(nameof(Lead), null));
        mapper.Register<GenericFlowEvent>();

        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.HttpCallOut));
        mapper.Register<SimpleActionMessage<HttpCallOutActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage message)
    {
        try
        {
            var task = message.Body switch
            {
                GenericFlowEvent generic when message.RoutingKey.StartsWith("object.") => GenericObjectEventAsync(message, generic),
                SimpleActionMessage<HttpCallOutActionOptions> http => HttpCallOutAsync(http),
                _ => null,
            };

            if (task != null) await task;
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

            // TODO: fire event
            // ...
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

    private async Task GenericObjectEventAsync(IMessage message, GenericFlowEvent evt)
    {
        if (evt.ObjectType != nameof(Lead)) return;

        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.Id, evt.TargetId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (lead == null)
        {
            Logger.LogError("{LeadId} not found or inactive", evt.TargetId);
            return;
        }

        // var organization = await _connection.Filter<Entity, Organization>()
        //     .Eq(x => x.AccountId, evt.AccountId)
        //     .Eq(x => x.Id, lead.EntityId)
        //     .Ne(x => x.IsActive, false)
        //     .FirstOrDefaultAsync();
        //
        // if (organization == null)
        // {
        //     Logger.LogError("{OrganizationId} not found or inactive", lead.EntityId);
        //     return;
        // }

        var subscriptions = await _connection.Filter<Subscription>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.ObjectType, nameof(Lead))
            .In(x => x.OrganizationId, new[] { default(Guid?), lead.EntityId })
            .AnyEq(x => x.Keys, nameof(FlowObjectEventRoute.Create))
            .FindAsync();

        var versions = new Dictionary<Guid, Dictionary<string, object>>();
        foreach (var subscription in subscriptions)
        {
            using var scope = Logger.AddScope(new
            {
                SubscriptionId = subscription.Id,
                subscription.EntityId,
                subscription.OrganizationId,
                subscription.Url,
            });

            Logger.LogInformation("Create Http Call Out");

            if (!versions.TryGetValue(subscription.ProfileId, out var flat))
            {
                var context = ProfileContext.Create(subscription.ProfileId, lead.AccountId, subscription.EntityId, subscription.ClientId, lead.EntityId);
                var objectType = await _objectTypeService.GetAsync(context, nameof(Lead));
                flat = await _objectTypeService.GetFlatObjectAsync(context, objectType, evt.TargetId);
                versions.Add(subscription.ProfileId, flat);
            }

            var refs = (evt.Refs ?? Enumerable.Empty<KeyValuePair<string, object>>())
                .Append(new KeyValuePair<string, object>(nameof(Lead), lead.Id))
                .Distinct(new KeyValueComparer())
                .ToList();

            var body = JsonConvert.SerializeObject(flat);
            var callout = new HttpCallOut
            {
                Id = Guid.NewGuid(),
                AccountId = subscription.AccountId,
                CreatedOn = DateTime.UtcNow,
                Request = new Request
                {
                    Method = Method.Post,
                    Url = subscription.Url,
                    Headers = new Dictionary<string, string[]>
                    {
                        { "Content-Type", new[] { "application/json" } },
                        { "Content-Length", new[] { Encoding.UTF8.GetBytes(body).Length.ToString() } },
                    },
                    Body = body,
                },
                Refs = refs,
            };

            await _connection.InsertAsync(callout);

            callout = await SendAsync(callout);
            if (callout != null)
            {
                Logger.LogInformation("Created {HttpCallOutId}: {StatusCode}", callout.Id, callout.Response?.StatusCode);
            }

            // TODO: fire event
            // ...
        }
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
            // .Set(x=>x.LastActor, )
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
                    // .Set(x=>x.LastActor, )
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Unset(x => x.RetryAfter)
                    .UpdateAndGetOneAsync();
            }
            else
            {
                var query = _connection.Filter<HttpCallOut>()
                        .Eq(x => x.Id, callout.Id)
                        .Update
                        // .Set(x=>x.LastActor, )
                        .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    ;

                if (callout.FailedAttempts?.Length > 3)
                {
                    // gives up
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