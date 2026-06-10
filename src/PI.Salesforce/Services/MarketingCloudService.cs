using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Salesforce.MarketingCloud;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services.DataProtection;

namespace Services;

public class MarketingCloudService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly DataProtectionService _dataProtectionService;
    private readonly MarketingCloudClient _client;
    private Dictionary<Guid, CachedToken> _cachedTokens = new();

    public MarketingCloudService(ILogger<MarketingCloudService> logger, IConfiguration configuration, IMessageBroker messageBroker,  
        MongoConnection connection,
        DataProtectionService dataProtectionService,
        MarketingCloudClient client 
        ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _dataProtectionService = dataProtectionService;
        _client = client;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.MarketingCloudDataExtension));
        mapper.Register<SimpleActionMessage<GenericActionOptions>>();
        mapper.Register<SimpleActionMessage<MarketingCloudDataExtensionActionOptions>>(); // should never happen but... 
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case SimpleActionMessage<GenericActionOptions> msg:
                    await ProcessMessageAsync(msg);
                    break;

                case SimpleActionMessage<MarketingCloudDataExtensionActionOptions> msg:
                    await ProcessMessageAsync(msg);
                    break;
            }
        }
        finally
        {
            evt.Acknowledge();
        }
    }

    private async Task ProcessMessageAsync(SimpleActionMessage<GenericActionOptions> action)
    {
        if (action.Options is not GenericActionOptions genericActionOptions)
        {
            Logger.LogError("Unexpected Options");
            return;
        }
        
        var options = genericActionOptions.ConvertTo<MarketingCloudDataExtensionActionOptions>();
        options.Output = genericActionOptions.Output;

        await ProcessMessageAsync(action.Event, options);
    }

    private async Task ProcessMessageAsync(SimpleActionMessage<MarketingCloudDataExtensionActionOptions> action) => await ProcessMessageAsync(action.Event, action.Options);

    private async Task ProcessMessageAsync(FlowEvent evt, MarketingCloudDataExtensionActionOptions options)
    {
        var error = default(string);
        try
        {
            var result = await ProcessAsync(evt, options);
            if (result.IsSuccess)
            {
                var successOut = options.Output.FirstOrDefault(x => x.Name == MarketingCloudDataExtensionActionOptions.UpsertedEventName);
                if (successOut?.EventId.HasValue ?? false)
                {
                    var successEvt = new GenericFlowEvent(evt)
                    {
                        Action = nameof(ActionIds.MarketingCloudDataExtension),
                        Description = successOut.Description,
                        EventTypeId = successOut.EventId,
                    };
                    // evt.AddRefValue(options.ObjectType, result.Value.ObjectId);
                    // evt.SetMetaValue($"Action|Output|{options.ObjectType}Id", result.Value.ObjectId);

                    await MessageBroker.DispatchAsync(successEvt);
                }
                return;
            }

            Logger.LogError("Upsert failed: {Error}", result.Status);
            error = result.Status;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to upsert dataextension");
            error = ex.Message;
        }
        
        // fire event
        var errorOut = options.Output.FirstOrDefault(x => x.Name == MarketingCloudDataExtensionActionOptions.FailedToUpsertEventName);
        if (errorOut?.EventId.HasValue ?? false)
        {
            var errorEvt = new GenericFlowEvent(evt)
            {
                Action = nameof(ActionIds.MarketingCloudDataExtension),
                Description = $"{errorOut.Description}. {error}",
                EventTypeId = errorOut.EventId,
            }; 
            
            await MessageBroker.DispatchAsync(errorEvt);
        }
    }
    
    private async Task<Result<string>> ProcessAsync(FlowEvent evt, MarketingCloudDataExtensionActionOptions options)
    {
        using var scope = Logger.AddScope(new
        {
            evt.AccountId,
            evt.RunId,
        });
        
        var accountContext = new AccountContext(evt.AccountId);
        var token = await GetTokenAsync(accountContext);
        if (token == null)
        {
            Logger.LogError("Failed to get Token");
            return Result.Error<string>("Failed to get Token");
        }
        
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.Id, evt.RunId)
            .FirstOrDefaultAsync();
        
        var runContext = flowRun.BuildHandlebarsContext(evt);
        
        var obj = default(object);
        
        if (!ExpressionEvaluatorService.TryResolve(accountContext, runContext, options.DataExtensionKey, out obj) || obj is not string dataExtensionKey)
        {
            return Result<string>.Error("Couldn't resolve External Id");
        }
        if (!ExpressionEvaluatorService.TryResolve(accountContext, runContext, options.PrimaryKeyField, out  obj) || obj is not string primaryKeyField)
        {
            return Result<string>.Error("Couldn't resolve Primary Key Field");
        }
        if (!ExpressionEvaluatorService.TryResolve(accountContext, runContext, options.PrimaryKey, out obj) || obj is not string primaryKey)
        {
            return Result<string>.Error("Couldn't resolve Primary Key Value");
        }

        var values = new Dictionary<string, object>();
        foreach (var kvp in options.Values)
        {
            if (!ExpressionEvaluatorService.TryResolve(accountContext, runContext, kvp.Value, out obj))
            {
                // return Result<string>.Error($"Couldn't resolve value: {kvp.Key}");
                continue;
            }
            
            values.Add(kvp.Key, obj);
        }
        
        var result = await _client.UpsertDataEventAsync(
            token.Token,
            dataExtensionKey,
            primaryKeyField,
            primaryKey,
            new MarketingCloudClient.UpsertDataExtensionRequest
            {
                Values = values,
            }
        );

        if (result?.Keys != null)
        {
            Logger.LogInformation("Upsert successful {DataExtensionExternalId}: {PrimaryKeyField}={PrimaryKey}", dataExtensionKey, primaryKeyField, primaryKey);
            return Result<string>.Success(primaryKey);
        }
        
        return Result<string>.Error("Upsert failed");
    }

    private async Task<CachedToken> GetTokenAsync(IEntityContext context)
    {
        if (_cachedTokens.TryGetValue(context.AccountId.Value, out var cachedToken))
        {
            if (cachedToken.ExpiresOn > DateTime.UtcNow)
            {
                return cachedToken;
            }

            Logger.LogInformation("Token has expired");
            _cachedTokens.Remove(context.AccountId.Value);
        }

        var integration = await _connection.Filter<MarketingCloudIntegrationConfiguration>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.EntityId.Value)
            .Eq(x => x.IntegrationId, IntegrationIds.MarketingCloud) // redundant as we will limit by type
            .FirstOrDefaultAsync();

        if (integration == null)
        {
            Logger.LogError("Marketing Cloud integration not configured");
            return null;
        }

        var clientSecret = await _dataProtectionService.UnprotectAsync(
            context,
            new MicrosoftDataProtectionConfig
            {
                Purpose = MarketingCloudIntegrationConfiguration.ProtectionKey,
            },
            integration.ClientSecret
        );
        
        // TODO: limit the scopes
        // ...
        var token = await _client.GetTokenAsync(integration.Subdomain, integration.ClientId, clientSecret);
        cachedToken = new CachedToken
        {
            Token = token,
        };

        _cachedTokens[context.AccountId.Value] = cachedToken;

        return cachedToken;
    }
    
    public class CachedToken
    {
        public DateTime ExpiresOn => Token.Expiration.AddSeconds(-120);
        public MarketingCloudToken Token { get; init; }
    }    
}

