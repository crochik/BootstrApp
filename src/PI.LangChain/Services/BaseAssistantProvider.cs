using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Extensions;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PI.LangChain.Models;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;
using ChatMessage = PI.LangChain.Models.ChatMessage;

namespace Services;

public abstract class BaseAssistantProvider : IAssistantProvider
{
    protected readonly ILogger _logger;
    protected readonly MongoConnection _connection;
    protected readonly DataProtectionService _dataProtectionService;
    protected readonly ObjectTypeService _objectTypeService;
    protected readonly DocumentTemplateService _documentTemplateService;
    
    public abstract Guid IntegrationId { get; }

    protected BaseAssistantProvider(
        ILogger logger,
        MongoConnection connection,
        DataProtectionService dataProtectionService,
        ObjectTypeService objectTypeService,
        DocumentTemplateService documentTemplateService
    )
    {
        _logger = logger;
        _connection = connection;
        _dataProtectionService = dataProtectionService;
        _objectTypeService = objectTypeService;
        _documentTemplateService = documentTemplateService;
    }

    protected string GuessContentType(Assistant assistant)
    {
        if (string.IsNullOrWhiteSpace(assistant.ObjectType))
        {
            return assistant.ResponseFormat switch
            {
                "json_object" => "application/json",
                _ => "text/plain"
            };
        }

        return "application/json";
    }

    protected async Task<ObjectType> GetObjectTypeAsync(IEntityContext context, Assistant assistant)
    {
        if (string.IsNullOrWhiteSpace(assistant.ObjectType))
            return null;

        var objectType = await _objectTypeService.GetAsync(context, assistant.ObjectType);
        if (objectType == null)
            throw NotFoundException.New($"Couldn't find {assistant.ObjectType}");

        return objectType;
    }

    protected object BuildGenericSchema(IEntityContext context, ObjectType objectType)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var kvp in objectType.Fields)
        {
            if (kvp.Value.InitialValue != null || string.IsNullOrEmpty(kvp.Value.Field?.Description))
                continue;

            switch (kvp.Value.Field)
            {
                case TextField textField:
                    properties[textField.Name] = new
                    {
                        type = "string",
                        description = textField.Description
                    };
                    if (!string.IsNullOrEmpty(textField.Description))
                        required.Add(textField.Name);
                    break;

                case SelectField selectField:
                    properties[selectField.Name] = new
                    {
                        type = "string",
                        description = selectField.Description,
                        @enum = selectField.SelectFieldOptions?.Items.Keys.ToEnumerableObject().OfType<string>().ToArray()
                    };
                    if (!string.IsNullOrEmpty(selectField.Description))
                        required.Add(selectField.Name);
                    break;
            }
        }

        return new
        {
            type = "object",
            properties = properties,
            required = required.ToArray(),
            additionalProperties = false
        };
    }

    protected string SerializeSchema(object schema)
    {
        return JsonConvert.SerializeObject(schema, new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    ProcessDictionaryKeys = false,
                }
            },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        });
    }

    protected Chat PrepareChat(IEntityContext context, Chat chat, Assistant assistant, IEnumerable<ChatMessage> additionalMessages, IDictionary<string, object> objectContext)
    {
        using var scope = _logger.AddScope(new
        {
            ChatId = chat.Id,
            AssistantId = assistant.Id,
            MessagesCount = chat.Messages?.Length,
        });

        if (assistant.Id != chat.Assistant.Id)
        {
            _logger.LogInformation("Different assistant, create new chat");
            chat = new Chat
            {
                AccountId = chat.AccountId,
                EntityId = chat.EntityId,
                Messages = chat.Messages,
                MetaValues = chat.MetaValues,
                Id = Guid.NewGuid(),
                CreatedOn = DateTime.UtcNow,
                LastActor = context.Actor(),
                Assistant = assistant,
                FlowId = assistant.ChatFlowId,
                ObjectStatusId = assistant.MapNewChatStatus(),
            };
        }
        else
        {
            _logger.LogInformation("Continue existing chat");
        }

        // add system prompt (if new assistant or there aren't any messages)
        if (chat.Messages == null || chat.Messages.Length == 0 || assistant.Id != chat.Assistant.Id)
        {
            var systemPrompt = _documentTemplateService.GetSystemPrompt(context, assistant, objectContext ?? new Dictionary<string, object>());
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                _logger.LogInformation("Add system prompt");

                chat.Messages = (chat.Messages ?? Enumerable.Empty<ChatMessage>())
                    .Append(new ChatMessage
                    {
                        Role = ChatRole.System,
                        ContentType = "text/plain",
                        Text = systemPrompt,
                    })
                    .ToArray();
            }
        }

        chat.Messages = (chat.Messages ?? Enumerable.Empty<ChatMessage>())
            .Concat(additionalMessages)
            .ToArray();

        return chat;
    }

    protected async Task WriteErrorToResponse(HttpResponse response, string message)
    {
        await response.WriteAsync($"event: error\n");
        await response.WriteAsync($"data: {JsonConvert.SerializeObject(new { Message = message })}\n\n");
        await response.Body.FlushAsync();
    }

    // Abstract methods that derived classes must implement
    public abstract Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Chat chat);
    public abstract Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Chat chat, Assistant assistant, IEnumerable<ChatMessage> additionalMessages, IDictionary<string, object> objectContext);
    public abstract Task<ChatMessage[]> GetCompletionStreamAsync(IEntityContext context, HttpResponse response, Chat chat);
}