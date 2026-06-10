using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Extensions;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.LangChain.Models;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Services;
using Services;
using ChatMessage = PI.LangChain.Models.ChatMessage;
using EmbeddedContent = PI.LangChain.Models.EmbeddedContent;

namespace PI.LangChain.Controllers;

public class AbstractChatController : APIController
{
    protected readonly ILogger<AbstractChatController> _logger;
    protected readonly MongoConnection _connection;
    protected readonly ObjectTypeService _objectTypeService;
    protected readonly AssistantCompletionService _assistantService;

    protected AbstractChatController(ILogger<AbstractChatController> logger, MongoConnection connection, ObjectTypeService objectTypeService, AssistantCompletionService assistantService)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _assistantService = assistantService;
    }

    protected async Task StreamChatAsync(IEntityContext context, Chat chat, Guid? remoteFileId = null, string userMessage = null, Guid? promptId = null)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("Connection", "keep-alive");

        if (!_assistantService.Providers.TryGetValue(chat.Assistant.IntegrationId, out var provider))
        {
            // error
            await Response.WriteAsync("event: error\n");
            await Response.WriteAsync($"data: {JsonConvert.SerializeObject(new { Message = $"Invalid Provider: {chat.Assistant.IntegrationId}" })}\n\n");
            await Response.Body.FlushAsync();
            await Response.CompleteAsync();
            return;
        }

        var objectType = await _objectTypeService.GetAsync(Context, chat.ObjectType);

        if (!promptId.HasValue && string.IsNullOrEmpty(userMessage) && !remoteFileId.HasValue && !string.IsNullOrEmpty(chat.Assistant.WelcomeMessage))
        {
            // welcome
            await Response.WriteAsync("event: welcome\n");
            await Response.WriteAsync($"data: {JsonConvert.SerializeObject(new { Content = $"{chat.Assistant.WelcomeMessage}" })}\n\n");

            await AddPromptsAsync(context, chat);

            await Response.WriteAsync("event: end\n");
            await Response.WriteAsync($"data: {JsonConvert.SerializeObject(new { ChatId = chat.Id })}\n\n");
            await Response.Body.FlushAsync();

            await Response.CompleteAsync();
            return;
        }

        var messages = Enumerable.Empty<ChatMessage>();

        if (promptId.HasValue)
        {
            var suggestedPrompt = await _connection.GetProfileElementAsync<ChatPrompt>(
                context,
                q =>
                    q
                        .Eq(x => x.Id, promptId.Value)
                        .In(x => x.EntityId, [null, context.AccountId, context.OrganizationId, context.UserId])
                        .Eq(x => x.AssistantId, chat.Assistant.Id)
            );

            if (suggestedPrompt?.Messages != null)
            {
                messages = messages.Concat(suggestedPrompt.Messages);
            }
        }

        if (!string.IsNullOrEmpty(userMessage))
        {
            messages = messages.Append(new ChatMessage
            {
                Role = ChatRole.User,
                Text = userMessage,
                ContentType = "text/plain",
            });
        }

        if (remoteFileId.HasValue)
        {
            messages = messages.Append(new ChatMessage
            {
                Role = ChatRole.User,
                // ContentType = "text/plain",
                RemoteFileId = remoteFileId,
            });
        }

        if (chat.Messages == null || chat.Messages.IsEmpty())
        {
            // "first time", add system prompt
            // TODO: this should be a properly calculated object context 
            // ...
            var objectContext = new Dictionary<string, object>
            {
                { "Object", chat },
                {
                    "Objects", new Dictionary<string, object>
                    {
                    }
                }
            };

            messages = _assistantService.AssistantService.BuildMessages(context, chat.Assistant, objectContext, messages).ToArray();
        }

        chat = await PushMessageAsync(chat, messages);

        var objectStatusId = chat.Assistant?.MapChatStatus(ChatStatus.Generating);
        if (objectStatusId.HasValue && chat.ObjectStatusId != objectStatusId.Value)
        {
            await UpdateObjectStatusAsync(objectType, chat, objectStatusId.Value);
            chat.ObjectStatusId = objectStatusId;
        }

        try
        {
            var assistantMessages = await provider.GetCompletionStreamAsync(context, Response, chat);

            var content = default(EmbeddedContent);
            foreach (var message in assistantMessages)
            {
                if (string.IsNullOrEmpty(message.Text)) continue;
                
                foreach (var line in message.Text.Split('\n'))
                {
                    if (line.StartsWith("```"))
                    {
                        if (content == null)
                        {
                            content = new EmbeddedContent
                            {
                                ContentType = line.Substring(3) switch
                                {
                                    "html" => "text/html",
                                    "json" => "application/json",
                                    _ => "text/plain",
                                },
                                Content = "",
                            };
                            continue;
                        }

                        // end
                        break;
                    }

                    if (content != null) content.Content += $"{line}\n";
                }
            }

            if (content != null)
            {
                chat.EmbeddedContent = content;
            }

            chat = await PushMessageAsync(chat, assistantMessages, updateEmbeddedContent: true);
            
            objectStatusId = chat.Assistant?.MapChatStatus(ChatStatus.Ready);
            if (objectStatusId.HasValue && chat.ObjectStatusId != objectStatusId.Value)
            {
                await UpdateObjectStatusAsync(objectType, chat, objectStatusId.Value);
                chat.ObjectStatusId = objectStatusId;
            }

            await AddPromptsAsync(context, chat);
        }
        catch (Exception ex)
        {
            // Handle any errors that occur during the API call
            await Response.WriteAsync("event: error\n");
            await Response.WriteAsync($"data: {JsonConvert.SerializeObject(new { Message = ex.Message })}\n\n");
            await Response.Body.FlushAsync();
        }
        finally
        {
            await Response.WriteAsync($"event: end\n");
            await Response.WriteAsync($"data: {JsonConvert.SerializeObject(new { ChatId = chat.Id })}\n\n");
            await Response.Body.FlushAsync();
            await Response.CompleteAsync();
        }
    }

    private async Task AddPromptsAsync(IEntityContext context, Chat chat)
    {
        // use status after update so eventually can react to flow changes to it
        if (!chat.ObjectStatusId.HasValue) return;

        var prompts = await _connection.GetProfileElementsAsync<ChatPrompt>(
            context,
            q =>
                q
                    .In(x => x.EntityId, [null, context.AccountId, context.OrganizationId, context.UserId])
                    .Eq(x => x.AssistantId, chat.Assistant.Id)
                    .OrBuilder(
                        or => or.Eq(x => x.ChatStatuses, null),
                        or => or.AnyEq(x => x.ChatStatuses, chat.ObjectStatusId.Value)
                    )
        );

        foreach (var prompt in prompts)
        {
            await Response.WriteAsync("event: prompt\n");
            await Response.WriteAsync($"data: {JsonConvert.SerializeObject(new { prompt.Id, prompt.Name, prompt.Description })}\n\n");
            await Response.Body.FlushAsync();
        }
    }

    private async Task<Chat> PushMessageAsync(Chat chat, IEnumerable<ChatMessage> messages, bool updateEmbeddedContent = false)
    {
        var query = _connection.Filter<Chat>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, Context.UserId.Value)
            .Eq(x => x.Id, chat.Id)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, Context.Actor);

        var modified = false;
        if (messages != null && !messages.IsEmpty())
        {
            query.PushEach(x => x.Messages, messages);
            modified = true;
        }

        if (updateEmbeddedContent)
        {
            query.SetOrUnset(x => x.EmbeddedContent, chat.EmbeddedContent);
            modified = true;
        }

        var modifiedChat = modified ? await query.UpdateAndGetOneAsync() : chat;

        // TODO: HANDLE EVENTS USING ACTION RUNNER SO WE CAN REACT TO THE RESULTS IN THE FLOW
        // ... 
        await _objectTypeService.FireObjectUpdatedAsync(Context, modifiedChat, new Dictionary<string, object> { { nameof(Chat.Messages), "*" } });

        return modifiedChat;
    }

    private async Task UpdateObjectStatusAsync(ObjectType objectType, Chat chat, Guid objectStatusId)
    {
        var isModified = await _objectTypeService.UpdateObjectStatusAsync(Context, objectType, chat.Id, objectStatusId);
        if (!isModified)
        {
            // nothing changed?
            return;
        }
        
        _logger.LogInformation("Successfully updated object status");

        // TODO: HANDLE EVENTS USING ACTION RUNNER SO WE CAN REACT TO THE RESULTS IN THE FLOW
        // ... 

        await _objectTypeService.DispatchAsync(new GenericFlowEvent(chat)
        {
            Action = nameof(ActionIds.SetObjectStatus),
            Description = "Status updated",
            EventTypeId = EventIds.OnStatusEntered,
        });
    }
}