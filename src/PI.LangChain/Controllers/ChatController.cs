using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PI.LangChain.Models;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Requests;
using Services;
using ChatMessage = PI.LangChain.Models.ChatMessage;

namespace PI.LangChain.Controllers;

[Route("/langchain/v1/[controller]")]
public class ChatController : APIController
{
    private readonly MongoConnection _connection;
    private readonly AssistantCompletionService _completionService;

    public ChatController(MongoConnection connection, AssistantCompletionService completionService)
    {
        _connection = connection;
        _completionService = completionService;
    }

    [Authorize("admin")]
    [HttpGet("/langchain/v1/[controller]({chatId})/DataForm")]
    [HttpGet("DataForm")]
    public async Task<Form> GetCompletionDataFormAsync([FromRoute] Guid chatId, [FromQuery] Guid? id)
    {
        id ??= chatId;

        var chat = await _connection.Filter<Chat>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.EntityId, Context.EntityId)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (chat == null)
        {
            return Form.BuildErrorForm("Chat not found");
        }

        var lastMessage = chat.Messages?.LastOrDefault();

        return new Form
        {
            Title = "Chat",
            Fields = fields().ToArray(),
            Actions = new[]
            {
                new FormAction
                {
                    Label = "Continue",
                    Action = "Add",
                    Enable = new[] { Form.RequiredFieldsName }
                }
            }
        };

        IEnumerable<FormField> fields()
        {
            yield return new ChildrenField
            {
                Name = "History",
                ChildrenFieldOptions = new ChildrenFieldOptions
                {
                    ObjectType = "ai.ChatMessage",
                    KeyType = ChildrenFieldOptions.IndexKeyType,
                    Url = $"/api/v1/CustomObject/ai.Chat({id})/Messages",
                },
                Enable = new[] { "false" }
            };

            if (lastMessage != null)
            {
                if (lastMessage.ContentType == "application/json")
                {
                    yield return new ObjectField
                    {
                        Name = "LastMessage",
                        Label = $"Last Message ({lastMessage.Role})",
                        DefaultValue = JsonConvert.DeserializeObject(lastMessage.Text),
                        ObjectFieldOptions = new ObjectFieldOptions
                        {
                            ObjectType = "*",
                        },
                        Enable = new[] { "false" },
                    };
                }
                else
                {
                    yield return new TextField
                    {
                        Name = "LastMessage",
                        Label = $"Last Message ({lastMessage.Role})",
                        DefaultValue = lastMessage?.Text,
                        TextFieldOptions = new TextFieldOptions
                        {
                            Multline = true,
                            ContentType = lastMessage?.ContentType,
                        },
                        Enable = new[] { "false" },
                    };
                }
            }

            yield return new ReferenceField
            {
                Name = nameof(Chat.Assistant),
                Label = "Assistant",
                IsRequired = true,
                DefaultValue = chat.Assistant.Id,
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = "ai.Assistant",
                }
            };

            yield return new TextField
            {
                Name = "UserMessage",
                Label = "Message",
                IsRequired = true,
                TextFieldOptions = new TextFieldOptions
                {
                    Multline = true,
                }
            };
        }
    }

    [Authorize("admin")]
    [HttpPost("/langchain/v1/[controller]({chatId})/DataForm")]
    [HttpPost("DataForm")]
    public async Task<DataFormActionResponse> CompletionDataFormAsync([FromRoute] Guid chatId, [FromQuery] Guid? id, [FromBody] DataFormActionRequest request)
    {
        id ??= chatId;

        var chat = await _connection.Filter<Chat>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.EntityId, Context.EntityId)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (chat == null)
        {
            return DataFormActionResponse.Error(request, "Chat not found");
        }

        if (!request.TryGetStrParam("UserMessage", out var userMessage))
        {
            return DataFormActionResponse.Error(request, "Missing required UserMessage parameter");
        }

        var assistant = chat.Assistant;
        if (request.TryGetGuidParam(nameof(Chat.Assistant), out var assistantId) && assistantId != assistant.Id)
        {
            assistant = await _connection.Filter<Assistant>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Id, assistantId)
                .FirstOrDefaultAsync();
        }

        var result = await _completionService.GetCompletionAsync(Context, chat, assistant,
            [
                new ChatMessage
                {
                    Text = userMessage,
                    ContentType = "text/plain",
                    Role = ChatRole.User
                }
            ]
        );

        if (!result.IsSuccess)
        {
            return DataFormActionResponse.Error(request, result.Status);
        }

        if (result.Value.Chat.Id == chat.Id)
        {
            await _connection.Filter<Chat>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Id, chat.Id)
                .ReplaceOneAsync(result.Value.Chat);
        }
        else
        {
            await _connection.InsertAsync(result.Value.Chat);
        }

        return new DataFormActionResponse(request)
        {
            Success = true,
            NextUrl = $"dataform:/langchain/v1/Chat({result.Value.Chat.Id})"
            // NextUrl = $"page:/GenAIContent?id={result.Value.Chat.Id}"
        };
    }
}