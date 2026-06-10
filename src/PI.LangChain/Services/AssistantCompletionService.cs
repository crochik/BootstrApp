using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Extensions;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using HandlebarsDotNet;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Newtonsoft.Json;
using PI.LangChain.Models;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;
using GenerativeAICompletionActionOptions = Messages.Flow.GenerativeAICompletionActionOptions;

namespace Services;

public class AssistantCompletionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    public AssistantService AssistantService { get; }
    public Dictionary<Guid, IAssistantProvider> Providers { get; }

    public AssistantCompletionService(ILogger<AssistantCompletionService> logger, IConfiguration configuration, IMessageBroker messageBroker, 
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        AssistantService assistantService,
        IEnumerable<IAssistantProvider> providers
    ) : base(logger, configuration, messageBroker)
    {
        AssistantService = assistantService;
        Providers = providers.ToDictionary(x => x.IntegrationId);

        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.GenerativeAICompletion));
        mapper.Register<SimpleActionMessage<GenericActionOptions>>();
        mapper.Register<SimpleActionMessage<GenerativeAICompletionActionOptions>>(); // should never happen but... 
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

                case SimpleActionMessage<GenerativeAICompletionActionOptions> msg:
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

        var options = genericActionOptions.ConvertTo<GenerativeAICompletionActionOptions>();
        options.Output = genericActionOptions.Output;

        await ProcessMessageAsync(action.Event, options);
    }

    private async Task ProcessMessageAsync(SimpleActionMessage<GenerativeAICompletionActionOptions> action) => await ProcessMessageAsync(action.Event, action.Options);

    private async Task ProcessMessageAsync(FlowEvent evt, GenerativeAICompletionActionOptions options)
    {
        var error = default(string);
        try
        {
            var result = await ProcessAsync(evt, options);
            if (result.IsSuccess)
            {
                var successOut = options.Output.FirstOrDefault(x => x.Name == GenerativeAICompletionActionOptions.OnCompletionEvent);
                if (successOut?.EventId.HasValue ?? false)
                {
                    var successEvt = new GenericFlowEvent(evt)
                    {
                        Action = nameof(ActionIds.GenerativeAICompletion),
                        Description = successOut.Description,
                        EventTypeId = successOut.EventId,
                    };

                    successEvt.AddRefValue(nameof(Chat), result.Value.Chat.Id);
                    successEvt.SetMetaValue($"Action|Output|{nameof(Chat)}Id", result.Value.Chat.Id);
                    successEvt.SetMetaValue($"Action|Output|Message", result.Value.Response);

                    await MessageBroker.DispatchAsync(successEvt);
                }

                return;
            }

            Logger.LogError("Upsert failed: {Error}", result.Status);
            error = result.Status;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get completion");
            error = ex.Message;
        }

        // fire event
        var errorOut = options.Output.FirstOrDefault(x => x.Name == GenerativeAICompletionActionOptions.OnFailedEvent);
        if (errorOut?.EventId.HasValue ?? false)
        {
            var errorEvt = new GenericFlowEvent(evt)
            {
                Action = nameof(ActionIds.GenerativeAICompletion),
                Description = $"{errorOut.Description}. {error}",
                EventTypeId = errorOut.EventId,
            };

            await MessageBroker.DispatchAsync(errorEvt);
        }
    }

    private async Task<Result<CompletionResponse>> ProcessAsync(FlowEvent evt, GenerativeAICompletionActionOptions options)
    {
        using var scope = Logger.AddScope(new
        {
            evt.AccountId,
            evt.RunId,
        });

        var accountContext = new AccountContext(evt.AccountId);
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.Id, evt.RunId)
            .FirstOrDefaultAsync();

        var runContext = flowRun.BuildHandlebarsContext(evt);

        var messages = Enumerable.Empty<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(options.SystemMessage))
        {
            var systemMessage = Handlebars.Compile(options.SystemMessage).Invoke(runContext);
            messages = messages.Append(new ChatMessage
            {
                ContentType = "text/plain",
                Role = ChatRole.System,
                Text = systemMessage,
            });
        }

        if (!string.IsNullOrWhiteSpace(options.UserMessage))
        {
            var userMessage = Handlebars.Compile(options.UserMessage).Invoke(runContext);
            messages = messages.Append(new ChatMessage
            {
                ContentType = "text/plain",
                Role = ChatRole.System,
                Text = userMessage,
            });
        }

        var values = new Dictionary<string, object>();
        if (options.Inputs != null)
        {
            foreach (var kvp in options.Inputs)
            {
                if (!ExpressionEvaluatorService.TryResolve(accountContext, runContext, kvp.Value, out var obj))
                {
                    continue;
                }

                values.Add(kvp.Key, obj);
            }
        }

        var chat = default(Chat);
        if (evt.ObjectType == "ai.Chat")
        {
            chat = await _connection.Filter<Chat>()
                .Eq(x => x.AccountId, accountContext.AccountId)
                .Eq(x => x.Id, evt.TargetId)
                .FirstOrDefaultAsync();

            if (chat == null) return Result.Error<CompletionResponse>("Failed to get chat");
        }

        var assistant = chat?.Assistant;
        if (ExpressionEvaluatorService.TryResolve(accountContext, runContext, options.AssistantId, out var assistantIdObj) && assistantIdObj != null)
        {
            if (!assistantIdObj.TryToParseObjectId(out var assistantId))
            {
                return Result<CompletionResponse>.Error("Invalid Assistant Value");
            }

            if (assistant?.Id != assistantId)
            {
                assistant = await _connection.Filter<Assistant>()
                    .Eq(x => x.AccountId, accountContext.AccountId)
                    .Ne(x => x.IsActive, false)
                    .Eq(x => x.Id, assistantId)
                    .FirstOrDefaultAsync();
            }
        }

        if (assistant == null)
        {
            return Result<CompletionResponse>.Error("Couldn't resolve Assistant");
        }

        var result = chat != null ? await GetCompletionAsync(accountContext, chat, assistant, messages.ToArray(), values) : await GetCompletionAsync(accountContext, assistant, messages.ToArray(), values);

        if (result.IsSuccess)
        {
            if (result.Value.ContentType == "application/json")
            {
                // TODO: add some indicator that this is what we want to do 
                // ...
                try
                {
                    IDictionary<string, object> json = JsonConvert.DeserializeObject<ExpandoObject>(result.Value.Response);
                    if (json.TryGetStrParam("ChatName", out var chatName))
                    {
                        result.Value.Chat.Name = chatName;
                    }

                    if (json.TryGetStrParam("ChatDescription", out var chatDescription))
                    {
                        result.Value.Chat.Description = chatDescription;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to parse response");
                }
            }

            if (chat == null || result.Value.Chat.Id != chat.Id)
            {
                // crete new chat
                await _connection.InsertAsync(result.Value.Chat);
            }
            else
            {
                // TODO: should update instead of replace?
                // ...
                // replace existing
                await _connection.Filter<Chat>()
                    .Eq(x => x.AccountId, chat.AccountId)
                    .Eq(x => x.Id, chat.Id)
                    .ReplaceOneAsync(result.Value.Chat);
            }

            await AddResponseToFlowRunAsync(accountContext, assistant, flowRun, options, result.Value);
        }

        return result;
    }

    private async Task AddResponseToFlowRunAsync(IEntityContext context, Assistant assistant, FlowRun flowRun, GenerativeAICompletionActionOptions options, CompletionResponse response)
    {
        var objectTypeName = options.NewObjectType ?? assistant.ObjectType;
        if (string.IsNullOrEmpty(objectTypeName) || response.ContentType is not ("application/json" or "application/schema+json"))
        {
            // nothing to do
            return;
        }

        var objectType = await _objectTypeService.GetAsync(context, objectTypeName);
        if (objectType == null)
        {
            Logger.LogError("Didn't find {ObjectType}", objectTypeName);
            throw NotFoundException.New(objectTypeName);
        }

        var added = await CreateObjectTypeAsync(context, options, response, objectType);
        if (!added.IsSuccess)
        {
            return;
        }

        await _objectTypeService.AddObjectToFlowRunAsync(context, objectType, added.Value.Object, flowRun, options.Alias);
    }

    private async Task<Result<ObjectTypeService.AddObjectResult>> CreateObjectTypeAsync(IEntityContext context, GenerativeAICompletionActionOptions options, CompletionResponse completion, ObjectType objectType)
    {
        var payload = JsonConvert.DeserializeObject<ExpandoObject>(completion.Response);
        return await _objectTypeService.AddObjectAsync(context, objectType, payload, new ObjectTypeService.AddObjectOptions
        {
            SkipObjectTypeValidation = true,
            OnBeforeSerializing = (dict) =>
            {
                var flowField = objectType.Fields.Values
                    .FirstOrDefault(x => x.Field is ReferenceField referenceField && referenceField.ReferenceFieldOptions?.ObjectType == nameof(Flow));
                if (flowField != null && options.NewObjectFlowId.HasValue)
                {
                    dict[flowField.Field.Name] = options.NewObjectFlowId.Value;
                }

                return Result.Success(dict);
            },
            PrepareEvent = evt =>
            {
                evt.AddRefValue("ai.Assistant", completion.Chat.Assistant.Id);
                evt.AddRefValue("ai.Chat", completion.Chat.Id);
            },
        });
    }

    public async Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Guid assistantId, string userMessage, IDictionary<string, object> objectContext)
    {
        var assistant = await _connection.Filter<Assistant>()
            .Eq(x => x.AccountId, context.AccountId)
            .Ne(x => x.IsActive, false)
            .Eq(x => x.Id, assistantId)
            .FirstOrDefaultAsync();

        if (assistant == null)
        {
            return Result<CompletionResponse>.Error("Assistant not found");
        }

        var messages = string.IsNullOrWhiteSpace(userMessage)
            ? Enumerable.Empty<ChatMessage>()
            : new ChatMessage
            {
                Role = ChatRole.User,
                ContentType = "text/plain",
                Text = userMessage,
            }.AsEnumerable();

        return await GetCompletionAsync(context, assistant, messages, objectContext);
    }

    private async Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Assistant assistant, IEnumerable<ChatMessage> messages, IDictionary<string, object> objectContext)
    {
        if (!Providers.TryGetValue(assistant.IntegrationId, out var provider))
        {
            return Result<CompletionResponse>.Error("Provider not supported");
        }

        var chat = AssistantService.BuildChat(context, assistant, objectContext, messages);
        return await provider.GetCompletionAsync(context, chat);
    }

    public async Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Chat chat, Assistant assistant, IEnumerable<ChatMessage> messages, IDictionary<string, object> objectContext = null)
    {
        if (!Providers.TryGetValue(assistant.IntegrationId, out var provider))
        {
            return Result<CompletionResponse>.Error("Provider not supported");
        }

        return await provider.GetCompletionAsync(context, chat, assistant, messages, objectContext);
    }
}