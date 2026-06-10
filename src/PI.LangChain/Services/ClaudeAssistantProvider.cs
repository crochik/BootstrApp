using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.LangChain.Models;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;
using ChatMessage = PI.LangChain.Models.ChatMessage;

namespace Services;

public class ClaudeAssistantProvider : BaseAssistantProvider
{
    public override Guid IntegrationId => IntegrationIds.Claude;

    private string GetModel(Assistant assistant)
    {
        return assistant.ModelProperties?.Model ?? assistant.Model ?? "claude-3-haiku-20240307";
    }

    public ClaudeAssistantProvider(
        ILogger<ClaudeAssistantProvider> logger,
        MongoConnection connection,
        DataProtectionService dataProtectionService,
        ObjectTypeService objectTypeService,
        DocumentTemplateService documentTemplateService
    ) : base(logger, connection, dataProtectionService, objectTypeService, documentTemplateService)
    {
    }

    private async Task<object> GetResponseFormat(IEntityContext context, Assistant assistant)
    {
        if (string.IsNullOrWhiteSpace(assistant.ObjectType))
        {
            return assistant.ResponseFormat switch
            {
                "json_object" => new { type = "json" },
                _ => new { type = "text" }
            };
        }

        var objectType = await GetObjectTypeAsync(context, assistant);
        var schema = BuildGenericSchema(context, objectType);
        
        return new
        {
            type = "json",
            json_schema = new
            {
                name = objectType.Name,
                description = objectType.Description,
                schema = schema,
                strict = true
            }
        };
    }


    public override Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Chat chat, Assistant assistant, IEnumerable<ChatMessage> additionalMessages, IDictionary<string, object> objectContext)
    {
        chat = PrepareChat(context, chat, assistant, additionalMessages, objectContext);
        return GetCompletionAsync(context, chat);
    }

    public override async Task<ChatMessage[]> GetCompletionStreamAsync(IEntityContext context, HttpResponse response, Chat chat)
    {
        var assistant = chat.Assistant;
        
        var apiKey = await GetClaudeAPIKeyAsync(context);
        if (!apiKey.IsSuccess)
        {
            await WriteErrorToResponse(response, $"Claude API key error: {apiKey.Status}");
            throw new Exception(apiKey.Status);
        }

        var client = new AnthropicClient(apiKey.Value);
        
        var messages = ConvertMessages(chat.Messages);
        var systemMessage = chat.Messages.FirstOrDefault(m => m.Role == ChatRole.System);
        var conversationMessages = messages.ToList();

        // Prepare system message
        var systemPrompt = string.Empty;
        if (systemMessage != null)
        {
            systemPrompt = systemMessage.Text;
        }

        // Add structured output format if specified
        var responseFormat = await GetResponseFormat(context, assistant);
        if (responseFormat != null && assistant.ResponseFormat == "json_object")
        {
            var schemaInstruction = $"\n\nPlease respond with valid JSON that matches this schema: {JsonConvert.SerializeObject(responseFormat)}";
            systemPrompt += schemaInstruction;
        }

        var assistantMessage = new ChatMessage
        {
            Role = ChatRole.Assistant,
            Text = "",
            ContentType = GuessContentType(assistant),
        };

        try
        {
            var request = new MessageParameters()
            {
                Messages = conversationMessages,
                Model = GetModel(assistant),
                MaxTokens = 4096,
                System = [new SystemMessage(systemPrompt)],
                Stream = true
            };

            await foreach (var streamChunk in client.Messages.StreamClaudeMessageAsync(request))
            {
                if (streamChunk?.Content != null)
                {
                    foreach (var content in streamChunk.Content.OfType<TextContent>())
                    {
                        var textChunk = content.Text;
                        if (!string.IsNullOrEmpty(textChunk))
                        {
                            assistantMessage.Text += textChunk;

                            await response.WriteAsync($"data: {JsonConvert.SerializeObject(new { Content = textChunk })}\n\n");
                            await response.Body.FlushAsync();
                        }
                    }

                    // Update token usage if available
                    if (streamChunk.Usage != null)
                    {
                        chat.InputTokens += streamChunk.Usage.InputTokens;
                        chat.OutputTokens += streamChunk.Usage.OutputTokens;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming completion from Claude API");
            await response.WriteAsync($"event: error\n");
            await response.WriteAsync($"data: {JsonConvert.SerializeObject(new { Message = $"Claude API error: {ex.Message}" })}\n\n");
            await response.Body.FlushAsync();
            throw;
        }

        return [assistantMessage];
    }
    
    public override async Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Chat chat)
    {
        var assistant = chat.Assistant;
        
        var apiKey = await GetClaudeAPIKeyAsync(context);
        if (!apiKey.IsSuccess) return apiKey.ConvertTo<CompletionResponse>();

        var client = new AnthropicClient(apiKey.Value);
        
        var messages = ConvertMessages(chat.Messages);
        var systemMessage = chat.Messages.FirstOrDefault(m => m.Role == ChatRole.System);
        var conversationMessages = messages.ToList();

        // Prepare system message
        var systemPrompt = string.Empty;
        if (systemMessage != null)
        {
            systemPrompt = systemMessage.Text;
        }

        // Add structured output format if specified
        var responseFormat = await GetResponseFormat(context, assistant);
        if (responseFormat != null && assistant.ResponseFormat == "json_object")
        {
            // Claude doesn't have direct structured output like OpenAI, so we'll include schema in system message
            var schemaInstruction = $"\n\nPlease respond with valid JSON that matches this schema: {JsonConvert.SerializeObject(responseFormat)}";
            systemPrompt += schemaInstruction;
        }

        try
        {
            var request = new MessageParameters()
            {
                Messages = conversationMessages,
                Model = GetModel(assistant),
                MaxTokens = 4096,
                System = [new SystemMessage(systemPrompt)],
            };

            var response = await client.Messages.GetClaudeMessageAsync(request);
            
            // Update token usage
            chat.InputTokens += response.Usage.InputTokens;
            chat.OutputTokens += response.Usage.OutputTokens;

            var responseText = string.Join('\n', response.Content.OfType<TextContent>() .Select(c => c.Text));
            
            var responseMessage = new ChatMessage
            {
                Role = ChatRole.Assistant,
                ContentType = GuessContentType(assistant),
                Text = responseText,
            };

            chat.Messages = chat.Messages.Append(responseMessage).ToArray();

            var result = new CompletionResponse
            {
                Response = responseText,
                ContentType = responseMessage.ContentType,
                ResponseFormat = assistant.ResponseFormat,
                Chat = chat,
            };

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completion from Claude API");
            return Result.Error<CompletionResponse>($"Claude API error: {ex.Message}");
        }
    }

    private Message[] ConvertMessages(ChatMessage[] messages)
    {
        var convertedMessages = new List<Message>();
        
        foreach (var message in messages)
        {
            // Skip system messages as they are handled separately in Claude API
            if (message.Role == ChatRole.System)
                continue;
                
            var role = message.Role switch
            {
                ChatRole.User => RoleType.User,
                ChatRole.Assistant => RoleType.Assistant,
                ChatRole.Tool => RoleType.User, // Claude doesn't have tool role, convert to user
                _ => RoleType.User
            };

            convertedMessages.Add(new Message
            {
                Role = role,
                Content = [new TextContent { Text = message.Text }]
            });
        }

        return convertedMessages.ToArray();
    }

    private async Task<Result<string>> GetClaudeAPIKeyAsync(IEntityContext context)
    {
        var integration = await _connection.Filter<ClaudeIntegrationConfiguration>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.AccountId.Value)
            .Eq(x => x.IntegrationId, IntegrationIds.Claude)
            .FirstOrDefaultAsync();

        if (integration == null) return Result.Error<string>("Claude integration not configured for account");

        var apiKey = await _dataProtectionService.UnprotectAsync(
            context,
            new MicrosoftDataProtectionConfig
            {
                Purpose = ClaudeIntegrationConfiguration.ProtectionKey,
            },
            integration.APIKey
        );

        return Result<string>.Success(apiKey);
    }
}