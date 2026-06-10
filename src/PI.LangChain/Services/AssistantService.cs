using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Extensions;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.LangChain.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class AssistantService
{
    private readonly ILogger<AssistantService> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly DocumentTemplateService _documentTemplateService;

    public AssistantService(
        ILogger<AssistantService> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        DocumentTemplateService documentTemplateService)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _documentTemplateService = documentTemplateService;
    }

    public async Task<Chat> CreateChatAsync(IEntityContext context, Guid assistantId, string userMessage, IDictionary<string, object> objectContext)
    {
        var assistant = await _connection.Filter<Assistant>()
            .Eq(x => x.AccountId, context.AccountId)
            .Ne(x => x.IsActive, false)
            .Eq(x => x.Id, assistantId)
            .FirstOrDefaultAsync();

        if (assistant == null)
        {
            throw new Exception("Assistant not found");
        }

        var chat = BuildChat(context, assistant, userMessage, objectContext);

        await _connection.InsertAsync(chat);
        await _objectTypeService.FireCreateEventAsync(context, chat);

        return chat;
    }

    private Chat BuildChat(IEntityContext context, Assistant assistant, string userMessage, IDictionary<string, object> objectContext)
    {
        var additionalMessages = string.IsNullOrWhiteSpace(userMessage)
            ? Enumerable.Empty<ChatMessage>()
            : new ChatMessage
            {
                Role = ChatRole.User,
                ContentType = "text/plain",
                Text = userMessage,
            }.AsEnumerable();

        return BuildChat(context, assistant, objectContext, additionalMessages);
    }

    public Chat BuildChat(IEntityContext context, Assistant assistant, IDictionary<string, object> objectContext, IEnumerable<ChatMessage> additionalMessages)
    {
        return new Chat
        {
            Id = Guid.NewGuid(),
            AccountId = context.AccountId.Value,
            EntityId = context.EntityId.Value,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            Assistant = assistant,
            Messages = BuildMessages(context, assistant, objectContext, additionalMessages).ToArray(),
            FlowId = assistant.ChatFlowId,
            ObjectStatusId = assistant.MapNewChatStatus(),
        };
    }

    public IEnumerable<ChatMessage> BuildMessages(IEntityContext context, Assistant assistant, IDictionary<string, object> objectContext, IEnumerable<ChatMessage> additionalMessages)
    {
        var systemPrompt = _documentTemplateService.GetSystemPrompt(context, assistant, objectContext);
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            yield return new ChatMessage
            {
                Role = ChatRole.System,
                ContentType = "text/plain",
                Text = systemPrompt,
            };
        }

        foreach (var msg in additionalMessages)
        {
            yield return msg;
        }
    }
}