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
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Http;
using PI.Shared.Models.OpenAPI;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;
using Method = PI.Shared.Models.Http.Method;
using Response = PI.Shared.Models.Http.Response;

namespace PI.OpenAPI.Services;

public class ActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly DataProtectionService _dataProtectionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private HttpClient Client => _httpClientFactory.CreateClient(nameof(ActionIds.OpenApiOperation));

    public ActionService(
        ILogger<ActionService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        DataProtectionService dataProtectionService,
        IHttpClientFactory httpClientFactory
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
        _dataProtectionService = dataProtectionService;
        _httpClientFactory = httpClientFactory;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.OpenApiOperation));
        mapper.Register<SimpleActionMessage<OpenApiOperationActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case SimpleActionMessage<OpenApiOperationActionOptions> action:
                    if (await ProcessAsync(action))
                    {
                        evt.Acknowledge();
                    }

                    break;
            }
        }
        finally
        {
            evt.Acknowledge();
        }
    }

    private async Task<bool> ProcessAsync(SimpleActionMessage<OpenApiOperationActionOptions> action)
    {
        using var scope = Logger.AddScope(new
        {
            action.Options?.OperationId,
            action.Event.RunId,
        });

        var error = default(string);
        try
        {
            var result = await ProcessOperationAsync(action);
            if (result.IsSuccess)
            {
                // TODO: fire event
                // ... 
                return true;
            }

            error = result.Status;
            Logger.LogError("Operation Failed: {Error}", error);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Operation failed");
            error = ex.Message;
        }

        // TODO: fire error event

        return false;
    }

    private async Task<Result<Response>> ProcessOperationAsync(SimpleActionMessage<OpenApiOperationActionOptions> action)
    {
        var operation = await _connection.Filter<Operation>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Options.OperationId)
            .FirstOrDefaultAsync();

        using var scope = Logger.AddScope(new
        {
            OperationId = operation.Id,
            OperationIdStr = operation.OperationId,
            operation.Namespace,
            Operation = operation.Name,
        });

        Logger.LogInformation("Prepare Operation");

        var document = await _connection.Filter<Document>()
            .Eq(x => x.AccountId, operation.AccountId)
            .Eq(x => x.Namespace, operation.Namespace)
            .FirstOrDefaultAsync();

        var accountContext = new AccountContext(operation.AccountId);

        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.RunId)
            .FirstOrDefaultAsync();

        var runContext = flowRun.BuildHandlebarsContext(action.Event);

        var headers = new Dictionary<string, string[]>
        {
            { "Accept", new[] { "application/json" } },
        };

        IEntityContext context = accountContext;
        if (document.IntegrationId.HasValue)
        {
            if (ExpressionEvaluatorService.TryResolve(context, runContext, action.Options.EntityId, out var entityIdObj))
            {
                var entityId = entityIdObj switch
                {
                    Guid guid => guid,
                    string str => Guid.TryParse(str, out var uuid) ? uuid : default(Guid?),
                    _ => default(Guid?),
                };

                if (entityId.HasValue)
                {
                    var entity = await _connection.Filter<Entity>()
                        .Eq(x => x.AccountId, context.AccountId)
                        .Eq(x => x.Id, entityId.Value)
                        .FirstOrDefaultAsync();

                    context = entity.Context;
                }
            }

            var accessTokenResult = await GetAccessTokenAsync(context, document.IntegrationId.Value);
            if (accessTokenResult.IsError)
            {
                Logger.LogError("Failed to get access token: {Error}", accessTokenResult.Status);
                return Result.Error<Response>($"Failed to get access token: {accessTokenResult.Status}");
            }

            var accessToken = accessTokenResult.Value;
            headers.Add("Authorization", new[] { $"Bearer {accessToken}" });
        }

        var body = default(string);

        if (operation.Request.Payloads != null && operation.Request.Payloads.TryGetValue("application/json", out var payload))
        {
            var bodyOt = await _objectTypeService.GetAsync(accountContext, payload.ObjectType);
            if (bodyOt == null)
            {
                Logger.LogError("Couldn't find {ObjectType} used in body", payload.ObjectType);
                return Result.Error<Response>("Failed to load body");
            }

            var resolvedBody = ExpressionEvaluatorService.TryResolveRecursively(context, runContext, action.Options.Request);
            if (resolvedBody.IsError)
            {
                Logger.LogError("Could not resolve object recursively: {Error}", resolvedBody.Status);
                return Result.Error<Response>(resolvedBody.Status);
            }

            body = JsonObjectConverter.SerializeObject(resolvedBody.Value);
            headers.Add("Content-Type", new[] { "application/json" });
        }
        else
        {
            Logger.LogInformation("There is no json request body");
        }

        Logger.LogInformation("Execute operation");

        if (!string.IsNullOrEmpty(body))
        {
            headers["Content-Length"] = new[] { Encoding.UTF8.GetBytes(body).Length.ToString() };
        }

        var url = $"{document.BaseUrl}{operation.Request.Path}";

        if (operation.Request.ParametersPlacement?.Count > 0)
        {
            // var parametersOt = await _objectTypeService.GetAsync(accountContext, operation.Request.ParametersObjectType);
            // if (parametersOt == null)
            // {
            //     Logger.LogError("Couldn't find {ObjectType} used in parameters", operation.Request.ParametersObjectType);
            //     return Result.Error<Response>("Failed to load parameters");
            // }

            var resolvedParams = ExpressionEvaluatorService.TryResolveRecursively(context, runContext, action.Options.Parameters);
            if (resolvedParams.IsError)
            {
                Logger.LogError("Could not resolve object recursively: {Error}", resolvedParams.Status);
                return Result.Error<Response>(resolvedParams.Status);
            }

            var parameters = resolvedParams.Value;
            foreach (var kvp in operation.Request.ParametersPlacement)
            {
                if (!parameters.TryGetValue(kvp.Key, out var value))
                {
                    Logger.LogInformation("Didn't find value for {Parameter}", kvp.Key);
                    value = null;
                }

                switch (kvp.Value)
                {
                    case "Path":
                        if (value == null) return Result.Error<Response>("Missing required part of path");
                        url = url.Replace("{" + kvp.Key + "}", value.ToString());
                        break;

                    case "Header":
                        // TODO: replace placeholders in the path
                        // ...
                        break;

                    default:
                        return Result.Error<Response>($"Unexpected Placement for Parameter {kvp.Key}: {kvp.Value}");
                }
            }
        }

        var callout = new HttpCallOut
        {
            Id = Guid.NewGuid(),
            AccountId = action.Event.AccountId,
            CreatedOn = DateTime.UtcNow,
            Request = new PI.Shared.Models.Http.Request
            {
                Method = operation.Request.Method switch
                {
                    "POST" => Method.Post,
                    "PUT" => Method.Put,
                    "GET" => Method.Get,
                    "PATCH" => Method.Patch,
                    "DELETE" => Method.Delete,
                    _ => throw new Exception($"Unexpected Method: {operation.Request.Method}"),
                },
                Url = url,
                Headers = headers,
                Body = body,
            },
            Refs = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>($"{nameof(FlowRun)}Id", action.Event.AccountId),
                new KeyValuePair<string, object>($"{action.Event.ObjectType}Id", action.Event.TargetId),
            }
        };

        var httpResponse = await Client.SendAsync(callout);

        Logger.LogInformation("{Method} {Url}: {Body} => {Status} {Response}", callout.Request.Method, callout.Request.Url, callout.Request.Body, httpResponse.StatusCode, httpResponse.Body);

        // save
        callout.Response = httpResponse;
        callout.Request.Headers?.Remove("Authorization");
        await _connection.InsertAsync(callout);

        var outputName = $"{(int)httpResponse.StatusCode}";
        if (!operation.Responses.TryGetValue(outputName, out var response))
        {
            Logger.LogError("Unexpected {StatusCode} for response", httpResponse.StatusCode);
            return Result.Error<Response>($"Unexpected response: {httpResponse.StatusCode}");
        }

        var evt = default(GenericFlowEvent);
        var actionOutput = action.Options.Output.FirstOrDefault(x => x.Name == outputName);
        if (actionOutput?.EventId.HasValue ?? false)
        {
            evt = new GenericFlowEvent(action.Event)
            {
                Action = nameof(ActionIds.OpenApiOperation),
                Description = $"{actionOutput.Description}",
                EventTypeId = actionOutput.EventId,
            };
        }

        if (!response.Payloads.TryGetValue(httpResponse.ContentType, out var respPayload))
        {
            Logger.LogError("Unexpected {ContentType}", httpResponse.ContentType);
            return Result.Error<Response>($"Unexpected response: {httpResponse.ContentType}");
        }

        if (!string.IsNullOrWhiteSpace(respPayload.ObjectType))
        {
            var respBody = JsonObjectConverter.DeserializeObject<ExpandoObject>(httpResponse.Body);
            var responseOt = await _objectTypeService.GetAsync(accountContext, respPayload.ObjectType);

            var obj = await _objectTypeService.AddObjectAsync(accountContext, responseOt, respBody, new ObjectTypeService.AddObjectOptions());

            if (obj.IsError)
            {
                Logger.LogError("Failed to parse response: {Status}", obj.Status);
                return Result.Error<Response>($"Failed to parse response: {obj.Status}");
            }

            Logger.LogInformation("{ObjectType} Created: {ObjectId}", responseOt.FullName, obj.Value.ObjectId);

            await _connection.Filter<FlowRun>()
                .Eq(x => x.AccountId, flowRun.AccountId)
                .Eq(x => x.Id, flowRun.Id)
                .Update
                .Set(
                    x => x.Objects[FlowRun.GetObjectAlias(action.Options.Alias ?? responseOt.FullName)],
                    new ObjectWithType
                    {
                        ObjectType = responseOt.FullName,
                        Object = await _objectTypeService.RecursivelyFlattenAsync(accountContext, responseOt, obj.Value.Object),
                    }
                )
                .UpdateOneAsync();
        }

        if (evt != null)
        {
            // fire event
            await MessageBroker.DispatchAsync(evt);
        }

        return Result.Success(httpResponse);
    }

    private async Task<Result<string>> GetAccessTokenAsync(IEntityContext context, Guid integrationId)
    {
        var integrationConfiguration = await _connection.Filter<IntegrationConfigurationWithToken>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.EntityId, context.GetOwnerEntityId())
            .Eq(x => x.IntegrationId, integrationId)
            .FirstOrDefaultAsync();

        if (integrationConfiguration == null) return Result.Error<string>("Integration not configured");

        var purpose = "EntityIntegration.Configuration";
        if (IntegrationIds.All.TryGetValue(integrationId, out var name))
        {
            purpose = $"EntityIntegration.{name}";
        }
        else
        {
            // TODO: load integration and get it from there 
            var integration = await _connection.Filter<Integration>()
                .Eq(x => x.Id, integrationId)
                .FirstOrDefaultAsync();
            // ...
        }
        
        if (integrationConfiguration.PersonalAccessToken != null)
        {
            try
            {
                var accessToken = await UnprotectAsync(context, integrationConfiguration.PersonalAccessToken, purpose);
                return Result.Success(accessToken);
            }
            catch (Exception ex)
            {
                return Result.Error<string>(ex.Message);
            }
        }

        if (integrationConfiguration.Token == null) return Result.Error<string>("Integration missing authentication");

        if (integrationConfiguration.Token.ExpiresOn < DateTime.UtcNow)
        {
            // TODO: refresh 
            // ...
        }

        try
        {
            var accessToken = await UnprotectAsync(context, integrationConfiguration.Token.AccessToken, purpose);
            return Result.Success(accessToken);
        }
        catch (Exception ex)
        {
            return Result.Error<string>(ex.Message);
        }
    }

    private async Task<string> UnprotectAsync(IEntityContext context, string protectedString, string purpose)
    {
        return await _dataProtectionService.UnprotectAsync(
            context,
            new MicrosoftDataProtectionConfig
            {
                Purpose = purpose,
            },
            protectedString
        );
    }
}