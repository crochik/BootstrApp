using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PI.LangChain.Models;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

/// <summary>
/// First attempt of aichat 
/// </summary>
[Obsolete("use app assistant/chat controller")]
[Route("/api/v1/[controller]")]
[Authorize("default")]
public class ContentController : APIController
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public ContentController(MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    /// <summary>
    /// Get Status/content of chat
    /// </summary>
    [Authorize("default")]
    [HttpGet("/api/v1/Chat({id})/[controller]")]
    public async Task<ChatContentResponse> GetChatContentAsync(Guid id)
    {
        var chat = await _connection.Filter<Chat>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, Context.UserId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (chat == null) throw NotFoundException.New("ai.Chat", id);

        return Map(chat);
    }

    /// <summary>
    /// Add user message to chat an trigger content generation
    /// </summary>
    [Authorize("default")]
    [HttpPost("/api/v1/Chat({id})/[controller]")]
    public async Task<ChatContentResponse> AddUserMessageToChatAsync(Guid id, [FromBody] AddMessageToChatRequest request)
    {
        var chat = await _connection.Filter<Chat>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, Context.UserId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (chat == null) throw NotFoundException.New("ai.Chat", id);

        if (
            chat.ObjectStatusId == chat.Assistant.ChatObjectStatusId ||
            !chat.Assistant.ChatObjectStatusId.HasValue ||
            chat.Messages == null ||
            chat.Messages.Length == 0
        )
        {
            throw new BadRequestException("Invalid status. Can't add chat message to chat");
        }

        chat = await _connection.Filter<Chat>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, Context.UserId.Value)
            .Eq(x => x.Id, id)
            .Ne(x => x.ObjectStatusId, chat.Assistant.ChatObjectStatusId.Value)
            .Update
            .Push(x => x.Messages, new ChatMessage
            {
                Role = ChatRole.User,
                Text = request.Message,
                ContentType = "text/plain",
            })
            .Set(x => x.LastActor, Context.Actor)
            .Set(x => x.LastModifiedOn, DateTime.Now)
            .Set(x => x.ObjectStatusId, chat.Assistant.ChatObjectStatusId.Value)
            .UpdateAndGetOneAsync();

        if (chat == null)
        {
            throw new BadRequestException("Failed to add message");
        }

        // TODO: fire update event?
        // ...

        await _objectTypeService.FireObjectStatusUpdatedAsync(Context, chat);

        return Map(chat);
    }

    /// <summary>
    /// Create chat (to generate content) using AI Assistant
    /// </summary>
    [Authorize("default")]
    [HttpPost("/api/v1/Assistant({id})/[controller]")]
    public async Task<ChatContentResponse> CreateChatAsync(Guid id, [FromQuery] string message)
        => await CreateChatAsync(id, null, null, message);

    /// <summary>
    /// Create chat (to generate content) using AI Assistant
    /// </summary>
    [Authorize("default")]
    [HttpPost("/api/v1/Assistant({id})/{objectType}({objectId})/[controller]")]
    public async Task<ChatContentResponse> CreateChatForObjectAsync(Guid id, [FromRoute] string objectType, [FromRoute] Guid? objectId, [FromQuery] string message)
        => await CreateChatAsync(id, objectType, objectId, message);

    private ChatContentResponse Map(Chat chat)
    {
        var busy = chat.ObjectStatusId == chat.Assistant.ChatObjectStatusId;
        var response = new ChatContentResponse
        {
            Id = chat.Id,
            IsBusy = busy,
            Messages = busy ? Array.Empty<ResolvedChatMessage>() : messages().ToArray(),
            Name = chat.Name,
            Description = chat.Description,
        };

        return response;

        IEnumerable<ResolvedChatMessage> messages()
        {
            if (chat.Messages == null) yield break;

            foreach (var input in chat.Messages)
            {
                if (input.Role == ChatRole.System) continue;

                if (input.Role == ChatRole.User)
                {
                    yield return new ResolvedChatMessage
                    {
                        Role = input.Role,
                        Prompt = input.Text,
                    };

                    continue;
                }

                // TODO: add a check based on object type
                // ...
                if (input.Role == ChatRole.Assistant && input.ContentType == "application/json")
                {
                    IDictionary<string, object> json = JsonConvert.DeserializeObject<ExpandoObject>(input.Text);
                    if (json.TryGetStrParam("AssistantFeedback", out var assistantFeedback) && json.TryGetStrParam("Content", out var content))
                    {
                        yield return new ResolvedChatMessage
                        {
                            Role = input.Role,
                            Prompt = assistantFeedback,
                            ContentType = chat.Assistant.ContentType,
                            Content = content,
                        };
                        continue;
                    }
                }

                yield return new ResolvedChatMessage
                {
                    Role = input.Role,
                    Prompt = "Generated Content",
                    ContentType = input.ContentType,
                    Content = input.Text,
                };
            }
        }
    }

    private async Task<ChatContentResponse> CreateChatAsync(Guid id, string objectType, Guid? objectId, string message)
    {
        var assistant = await _connection.GetProfileElementAsync<Assistant>(Context, q => q
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, id)
        );

        if (assistant == null)
        {
            throw new NotFoundException("Assistant not found");
        }

        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.UtcNow,
            AccountId = Context.AccountId.Value,
            EntityId = Context.UserId.Value,
            Assistant = assistant,
            FlowId = assistant.ChatFlowId,
            ObjectStatusId = assistant.ChatObjectStatusId,
        };

        // TODO: use the synchronous flow handler (like lms)?
        // ...
        chat = await _objectTypeService.InsertAsync(Context, chat, e =>
        {
            if (!string.IsNullOrWhiteSpace(objectType))
            {
                e.SetMetaValue(objectType, objectId);
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                // figure out a good way to pass it along to be added as the first user message
                // ...
            }
        });

        return Map(chat);
    }
}

public class ResolvedChatMessage
{
    public string ContentType { get; set; }
    public string Content { get; set; }
    public string Prompt { get; set; }
    public ChatRole Role { get; set; }
}

/// <summary>
/// Simplified Chat model
/// </summary>
public class ChatContentResponse
{
    public Guid Id { get; set; }

    /// <summary>
    /// Conversation so far
    /// </summary>
    public ResolvedChatMessage[] Messages { get; set; }

    /// <summary>
    /// Whether the assistant is busy calculating next content
    /// (any time the last message is not by the assistant?)
    /// </summary>
    public bool IsBusy { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }
}

public class AddMessageToChatRequest
{
    /// <summary>
    /// User Message
    /// </summary>
    public string Message { get; set; }

    // files, ...
    // ...

    // references, ...
    // ...
}