using System.Collections.Generic;
using System;
using System.ClientModel;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Extensions;
using Crochik.Mongo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI.Chat;
using PI.LangChain.Models;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;
using ChatMessage = PI.LangChain.Models.ChatMessage;

namespace Services;

public class OpenAIAssistantProvider : BaseAssistantProvider
{
    public override Guid IntegrationId => IntegrationIds.OpenAI;

    private string GetModel(Assistant assistant)
    {
        return assistant.ModelProperties?.Model ?? assistant.Model;
    }

    public OpenAIAssistantProvider(
        ILogger<OpenAIAssistantProvider> logger,
        MongoConnection connection,
        DataProtectionService dataProtectionService,
        ObjectTypeService objectTypeService,
        DocumentTemplateService documentTemplateService
    ) : base(logger, connection, dataProtectionService, objectTypeService, documentTemplateService)
    {
    }


    private async ValueTask<ChatResponseFormat> GetResponseFormat(IEntityContext context, Assistant assistant)
    {
        if (string.IsNullOrWhiteSpace(assistant.ObjectType))
        {
            return assistant.ResponseFormat switch
            {
                "json_object" => ChatResponseFormat.CreateJsonObjectFormat(),
                _ => ChatResponseFormat.CreateTextFormat(),
            };
        }

        var objectType = await GetObjectTypeAsync(context, assistant);
        var schema = BuildOpenAISchema(context, objectType);
        var outputString = SerializeSchema(schema);

        return ChatResponseFormat.CreateJsonSchemaFormat(
            objectType.Name,
            BinaryData.FromString(outputString),
            objectType.Description,
            true
        );
    }

    /// <summary>
    /// https://platform.openai.com/docs/guides/structured-outputs/how-to-use
    /// </summary>
    private RootSchema BuildOpenAISchema(IEntityContext context, ObjectType objectType)
    {
        return new RootSchema
        {
            Name = objectType.Name,
            Description = objectType.Description,
            Properties = getProperties().ToDictionary(x => x.Name)
        };

        IEnumerable<OpenApiSchema> getProperties()
        {
            foreach (var kvp in objectType.Fields)
            {
                if (kvp.Value.InitialValue != null || string.IsNullOrEmpty(kvp.Value.Field?.Description)) continue;

                switch (kvp.Value.Field)
                {
                    case TextField textField:
                        yield return new StringSchema
                        {
                            Name = textField.Name,
                            Description = textField.Description,
                        };
                        break;

                    case SelectField selectField:
                        yield return new EnumSchema
                        {
                            Name = selectField.Name,
                            Description = selectField.Description,
                            Enum = selectField.SelectFieldOptions?.Items.Keys.ToEnumerableObject().OfType<string>().ToArray(),
                        };
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Continue existing chat 
    /// </summary>
    public override Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Chat chat, Assistant assistant, IEnumerable<ChatMessage> additionalMessages, IDictionary<string, object> objectContext)
    {
        chat = PrepareChat(context, chat, assistant, additionalMessages, objectContext);
        return GetCompletionAsync(context, chat);
    }

    // /// <summary>
    // /// start new chat
    // /// </summary>
    // public Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Assistant assistant, IEnumerable<ChatMessage> additionalMessages, IDictionary<string, object> objectContext)
    // {
    //     var chat = new Chat
    //     {
    //         Id = Guid.NewGuid(),
    //         AccountId = context.AccountId.Value,
    //         EntityId = context.EntityId.Value,
    //         CreatedOn = DateTime.UtcNow,
    //         LastActor = context.Actor(),
    //         Assistant = assistant,
    //         Messages = messages().ToArray(),
    //         FlowId = assistant.ChatFlowId,
    //         ObjectStatusId = assistant.ChatObjectStatusId,
    //     };
    //
    //     return GetCompletionAsync(context, assistant, chat);
    //
    //     IEnumerable<ChatMessage> messages()
    //     {
    //         var systemPrompt = _documentTemplateService.GetSystemPrompt(context, assistant, objectContext);
    //         if (!string.IsNullOrWhiteSpace(systemPrompt))
    //         {
    //             yield return new ChatMessage
    //             {
    //                 Role = ChatRole.System,
    //                 ContentType = "text/plain",
    //                 Text = systemPrompt,
    //             };
    //         }
    //
    //         foreach (var msg in additionalMessages)
    //         {
    //             yield return msg;
    //         }
    //     }
    // }

    private async Task<List<ChatTool>> GetTools(IEntityContext context, Assistant assistant)
    {
        await Task.CompletedTask;
        return getTools().ToList();

        IEnumerable<ChatTool> getTools()
        {
            yield break;

            yield return ChatTool.CreateFunctionTool(
                "filter-leads",
                """
                Get List of leads that match criterion. Avoid making multiple calls. Combine all conditions in one call.
                Use #text field when looking for partial names.
                Fields schema:
                location:
                    description: GeoJSON Point 
                    type: object
                    properties:
                      coordinates:
                        title: coordinates[0]=longitude, coordinates[1]=latitude
                        type: array
                        items:
                          type: number
                locationDistance:
                    description: distance in meters
                    type: number
                #text:
                    description: words in the lead name
                    type: string
                """,
                BinaryData.FromBytes("""
                                     {
                                       "type": "object",
                                       "properties": {
                                         "criteria": {
                                           "title": "Filter Criterion",
                                           "description": "All conditions that a lead must satisfy to be included.",
                                           "type": "array",
                                           "items": {
                                             "type": "object",
                                             "properties": {
                                               "fieldName": {
                                                 "title": "Lead: Filterable Fields",
                                                 "type": "string",
                                                 "description": "Filterable fields for Lead",
                                                 "enum": [
                                                   "_id",
                                                   "#text",
                                                   "convertedDate",
                                                   "createdDate",
                                                   "flowId",
                                                   "integrations_externalId",
                                                   "integrations_integrationId",
                                                   "integrations_tag",
                                                   "isActive",
                                                   "lastModifiedDate",
                                                   "leadTypeId",
                                                   "Location",
                                                   "LocationDistance",
                                                   "objectStatusId",
                                                   "organizationId",
                                                   "phone",
                                                   "Refs|v",
                                                   "tags",
                                                   "userId"
                                                 ]
                                               },
                                               "operator": {
                                                 "title": "Condition Operator",
                                                 "enum": [
                                                   "Eq",
                                                   "Gt",
                                                   "Gte",
                                                   "In",
                                                   "Lt",
                                                   "Lte",
                                                   "Ne",
                                                   "Nin"
                                                 ],
                                                 "type": "string",
                                                 "description": "System: Condition Operator"
                                               },
                                               "value": {
                                                 "title": "Value",
                                                 "description": "Value to be used in the filter operation"
                                               }
                                             }
                                           }
                                         }
                                       }
                                     }
                                     """u8.ToArray()));

            yield return ChatTool.CreateFunctionTool(
                "send-email",
                "Send an email message. Do not call until the user has confirmed the subject, recipient and content",
                BinaryData.FromBytes("""
                                     {
                                         "type": "object",
                                         "properties":
                                         {
                                             "recipient":
                                             {
                                                 "type": "string",
                                                 "format": "email", 
                                                 "description": "The recipient's email address."
                                             },
                                             "subject":
                                             {
                                                 "type": "string",
                                                 "description": "The subject of the email."
                                             },
                                             "body":
                                             {
                                                 "type":  "string",
                                                 "description": "The body of the email (html)"
                                             }
                                         },
                                         "required": [ "recipient", "subject", "body" ]
                                     }
                                     """u8.ToArray()));

            //     yield return new ChatTool.CreateFunctionalTool()
            //     {
            //         Name = "send_email",
            //         Description = "Sends an email to a recipient.",
            //         Parameters = JsonSchema.Parse(JsonSerializer.Serialize(new
            //         {
            //             Type = "object",
            //             Properties = new
            //             {
            //                 Recipient = new
            //                 {
            //                     Type = "string",
            //                     Format = "email", // Use "email" format for validation.
            //                     Description = "The recipient's email address.",
            //                 },
            //                 Subject = new
            //                 {
            //                     Type = "string",
            //                     Description = "The subject of the email.",
            //                 },
            //                 Body = new
            //                 {
            //                     Type = "string",
            //                     Description = "The body of the email.",
            //                 },
            //             },
            //             Required = new[] { "recipient", "subject", "body" },
            //         };
        }
    }

    private async Task<ChatCompletionOptions> BuildOptions(IEntityContext context, Assistant assistant)
    {
        ChatCompletionOptions options = new()
        {
            ResponseFormat = await GetResponseFormat(context, assistant),
            // Seed =
            // Temperature =
            // ReasoningEffortLevel = ChatReasoningEffortLevel.Minimal,
        };

#pragma warning disable OPENAI001
        if (assistant.ModelProperties != null && assistant.ModelProperties.ThinkingLevel.HasValue)
        {
            _logger.LogInformation("Setting {Model}: {ThinkingLevel}", assistant.ModelProperties.Model, assistant.ModelProperties.ThinkingLevel);

            options.ReasoningEffortLevel = assistant.ModelProperties.ThinkingLevel switch
            {
                ModelThinkingLevel.Low => ChatReasoningEffortLevel.Minimal,
                ModelThinkingLevel.Medium => ChatReasoningEffortLevel.Medium,
                ModelThinkingLevel.High => ChatReasoningEffortLevel.High,
                _ => ChatReasoningEffortLevel.Low,
            };
        }
#pragma warning restore OPENAI001

        // TODO: probably makes sense to have tools become available/unavailable dynamically
        // could have a tool that allows loading other tools?
        // some other "step" in the flow could decide to make tools available to to the chat 
        // ...
        var tools = await GetTools(context, assistant);
        if (tools != null && !tools.IsEmpty())
        {
            // TODO: make whether the assistant will define 
            options.ToolChoice = ChatToolChoice.CreateAutoChoice();

            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }
        }

        return options;
    }

    public override async Task<ChatMessage[]> GetCompletionStreamAsync(IEntityContext context, HttpResponse response, Chat chat)
    {
        var chatResultStream = await GetCompletionStreamAsync(context, chat);

        var assistantMessage = new ChatMessage
        {
            Role = ChatRole.Assistant,
            Text = "",
            ContentType = "text/plain",
        };

        await foreach (var chatResult in chatResultStream)
        {
            foreach (var update in chatResult.ContentUpdate)
            {
                switch (update.Kind)
                {
                    case ChatMessageContentPartKind.Text:
                    {
                        assistantMessage.Text += update.Text;

                        await response.WriteAsync($"data: {JsonConvert.SerializeObject(new { Content = update.Text })}\n\n");
                        await response.Body.FlushAsync();
                        break;
                    }
                }
            }

            // TODO: how to handle tool calls
            // ...

            switch (chatResult.FinishReason)
            {
                case ChatFinishReason.Stop:
                    break;
                case ChatFinishReason.Length:
                case ChatFinishReason.ContentFilter:
                    break;
                case ChatFinishReason.ToolCalls:
                case ChatFinishReason.FunctionCall:
                    break;
            }
        }

        return [assistantMessage];
    }

    public async Task<AsyncCollectionResult<StreamingChatCompletionUpdate>> GetCompletionStreamAsync(IEntityContext context, Chat chat)
    {
        var apiKey = await GetOpenAIAPIKeyAsync(context);
        if (!apiKey.IsSuccess)
        {
            throw new Exception(apiKey.Status);
        }

        var client = new ChatClient(model: GetModel(chat.Assistant), apiKey.Value);
        var options = await BuildOptions(context, chat.Assistant);

        return client.CompleteChatStreamingAsync(chat.Messages.Select(FromModel), options);
    }

    public override async Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Chat chat)
    {
        var assistant = chat.Assistant;

        var apiKey = await GetOpenAIAPIKeyAsync(context);
        if (!apiKey.IsSuccess) return apiKey.ConvertTo<CompletionResponse>();

        var model = GetModel(assistant);
        ChatClient client = new(model: model, apiKey.Value);

        var options = await BuildOptions(context, assistant);
        var start = DateTime.UtcNow;
        ChatCompletion completion = await client.CompleteChatAsync(chat.Messages.Select(FromModel), options);
        _logger.LogInformation("{Model} {ChatId}: took {Duration} ms", model, chat.Id, (DateTime.UtcNow - start).TotalMilliseconds);

        chat.InputTokens += completion.Usage.InputTokenCount;
        chat.OutputTokens += completion.Usage.OutputTokenCount;

        // TODO: handle kind!=Text?
        // ...
        var response = new ChatMessage
        {
            Role = ChatRole.Assistant,
            ContentType = GuessContentType(assistant),
            Text = string.Join('\n', completion.Content
                .Where(x => x.Kind == ChatMessageContentPartKind.Text)
                .Select(x => x.Text)
            ),
        };

        chat.Messages = chat.Messages.Append(response).ToArray();

        var result = new CompletionResponse
        {
            Response = response.Text,
            ContentType = response.ContentType,
            ResponseFormat = assistant.ResponseFormat,
            Chat = chat,
        };

        switch (completion.FinishReason)
        {
            case ChatFinishReason.Length:
            case ChatFinishReason.ContentFilter:
            case ChatFinishReason.FunctionCall:
                throw new Exception($"Failed to get completion: {completion.FinishReason}");
                break;

            case ChatFinishReason.ToolCalls:
                break;

            case ChatFinishReason.Stop:
                // normal
                break;
        }

        return Result.Success(result);
    }

    void RunTools(ChatCompletion completion, ChatMessage response)
    {
        response.ToolCalls = completion.ToolCalls.Select(x => new ExecuteTool
        {
            Name = x.FunctionName,
            Id = x.Id,
            Arguments = x.FunctionArguments.ToString(),
        }).ToArray();

        // run tools? 
        foreach (var toolCall in completion.ToolCalls)
        {
            _logger.LogWarning("Execute {Tool}: {Csriteria}", toolCall.FunctionName, toolCall.FunctionArguments.ToString());
            // toolCall.FunctionName
            // toolCall.FunctionArguments

            // TODO: add result from the tool to the messages
            // ...

            // messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
        }

        // TODO: make another call to the completion service?
        // it may need to be deferred as some of the tools may need to be executed via rmq message
        // probably makes sense to use the action runner here so some can be handled synchronously
        // may need to add a flag to the chat indicating that is still pending "tool execution" 
        // ...
    }


    private OpenAI.Chat.ChatMessage FromModel(ChatMessage message)
    {
        return message.Role switch
        {
            ChatRole.Assistant => OpenAI.Chat.ChatMessage.CreateAssistantMessage(message.Text),
            ChatRole.System => OpenAI.Chat.ChatMessage.CreateSystemMessage(message.Text),
            ChatRole.User => OpenAI.Chat.ChatMessage.CreateUserMessage(message.Text),
            ChatRole.Tool => OpenAI.Chat.ChatMessage.CreateToolMessage(message.Text),
            _ => OpenAI.Chat.ChatMessage.CreateUserMessage(message.Text),
        };
    }

    private async Task<Result<string>> GetOpenAIAPIKeyAsync(IEntityContext context)
    {
        var integration = await _connection.Filter<OpenAIIntegrationConfiguration>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.AccountId.Value)
            .Eq(x => x.IntegrationId, IntegrationIds.OpenAI) // redundant as we will limit by type
            .FirstOrDefaultAsync();

        if (integration == null) return Result.Error<string>("OpenAI integration not configured for account");

        var apiKey = await _dataProtectionService.UnprotectAsync(
            context,
            new MicrosoftDataProtectionConfig
            {
                Purpose = OpenAIIntegrationConfiguration.ProtectionKey,
            },
            integration.APIKey
        );

        return Result<string>.Success(apiKey);
    }
}