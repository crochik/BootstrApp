using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GenerativeAI;
using Crochik.Mongo;
using GenerativeAI.Types;
using LangChain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.LangChain.Models;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Files;
using PI.Shared.Models.Interfaces;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;
using ChatMessage = PI.LangChain.Models.ChatMessage;

namespace Services;

public class GeminiAssistantProvider : BaseAssistantProvider
{
    private readonly RemoteFileService _remoteFileService;
    private readonly IHttpClientFactory _httpClientFactory;
    private HttpClient Client => _httpClientFactory.CreateClient("Gemini");
    public override Guid IntegrationId => IntegrationIds.Gemini;

    private string GetModel(Assistant assistant) => assistant.ModelProperties?.Model ?? assistant.Model ?? "gemini-2.5-flash-lite";
    
    private bool CanModelProduceImages(Assistant assistant)
    {
        var model = GetModel(assistant);
        return assistant.ModelProperties?.ImageCapability ?? model.Contains("-image", StringComparison.Ordinal);
    }

    public GeminiAssistantProvider(
        ILogger<GeminiAssistantProvider> logger,
        MongoConnection connection,
        DataProtectionService dataProtectionService,
        ObjectTypeService objectTypeService,
        DocumentTemplateService documentTemplateService,
        RemoteFileService remoteFileService,
        IHttpClientFactory httpClientFactory
    ) : base(logger, connection, dataProtectionService, objectTypeService, documentTemplateService)
    {
        _remoteFileService = remoteFileService;
        _httpClientFactory = httpClientFactory;
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
            schema = schema
        };
    }

    public override Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Chat chat, Assistant assistant, IEnumerable<ChatMessage> additionalMessages, IDictionary<string, object> objectContext)
    {
        chat = PrepareChat(context, chat, assistant, additionalMessages, objectContext);
        return GetCompletionAsync(context, chat);
    }

    private async Task<Result<GoogleAi>> GetClientAsync(IEntityContext context)
    {
        var apiKey = await GetGeminiApiKeyAsync(context);
        if (!apiKey.IsSuccess)
        {
            return apiKey.ConvertTo<GoogleAi>();
        }


        var client = new GoogleAi(apiKey.Value, client: Client, logger: _logger);
        return Result.Success(client);
    }

    public override async Task<ChatMessage[]> GetCompletionStreamAsync(IEntityContext context, HttpResponse response, Chat chat)
    {
        var client = await GetClientAsync(context);
        if (!client.IsSuccess)
        {
            await WriteErrorToResponse(response, $"Gemini Error: {client.Status}");
            throw new Exception(client.Status);
        }

        var request = await CreateRequestAsync(context, chat);
        if (!request.IsSuccess)
        {
            await WriteErrorToResponse(response, $"Error Building Request: {request.Status}");
            throw new Exception(request.Status);
        }

        var assistant = chat.Assistant;

        var result = new List<ChatMessage>();
        ChatMessage assistantMessage = null;
        try
        {
            var model = client.Value.CreateGenerativeModel(GetModel(assistant));
            var streamResponse = model.StreamContentAsync(request.Value);
            
            await foreach (var chunk in streamResponse)
            {
                if (chunk.Candidates == null || chunk.Candidates.Length == 0)
                {
                    // var message = $"{(DateTime.Now - start).TotalMilliseconds}: no candidates?";
                    // assistantMessage.Text += message;
                    // await response.WriteAsync($"data: {JsonConvert.SerializeObject(new { Content = message })}\n\n");
                    // await response.Body.FlushAsync();
                    continue;
                }

                foreach (var candidate in chunk.Candidates)
                {
                    if (candidate.Content?.Parts == null || candidate.Content.Parts.Count == 0)
                    {
                        // var message = $"DEBUG: No parts\n";
                        // assistantMessage.Text += message;
                        // await response.WriteAsync("event: status\n");
                        // await response.WriteAsync($"data: {JsonConvert.SerializeObject(new { Content = message })}\n\n");
                        // await response.Body.FlushAsync();
                        continue;
                    }

                    foreach (var part in candidate.Content.Parts)
                    {
                        if (part.Text != null)
                        {
                            if (assistantMessage == null)
                            {
                                assistantMessage ??= new ChatMessage
                                {
                                    Role = ChatRole.Assistant,
                                    Text = "",
                                    ContentType = GuessContentType(assistant),
                                    ThoughtSignature = part.ThoughtSignature,
                                };
                                result.Add(assistantMessage);
                            }
                            else
                            {
                                assistantMessage.Text += chunk.Text;
                            }

                            await response.WriteAsync($"data: {JsonConvert.SerializeObject(new { Content = chunk.Text })}\n\n");
                            await response.Body.FlushAsync();
                        }

                        if (part.InlineData?.Data != null)
                        {
                            // await response.WriteAsync("event: document\n");
                            // await response.WriteAsync($"data: {JsonConvert.SerializeObject(new { ContentType = part.InlineData.MimeType, part.InlineData.Data.Length })}\n\n");
                            // await response.Body.FlushAsync();

                            if (assistant.UploadFileOptions?.RemoteFolderId == null)
                            {
                                // error: can't save file
                                await response.WriteAsync("event: error\n");
                                await response.WriteAsync($"data: {JsonConvert.SerializeObject(new { Message = "Assistant not configured to upload files\n" })}\n\n");
                                await response.Body.FlushAsync();
                                continue;
                            }

                            var imageData = Convert.FromBase64String(part.InlineData.Data);
                            using var stream = new MemoryStream(imageData);
                            var remoteFile = await _remoteFileService.UploadAsync(context, stream, part.InlineData.MimeType, $"Chat{chat.Id}_{DateTime.UtcNow:MMddyyyyHHmmss}", assistant.UploadFileOptions);
                            remoteFile.Parent = new ReferencedObject
                            {
                                ObjectId = chat.Id,
                                ObjectType = "ai.Chat",
                            };

                            await _connection.InsertAsync(remoteFile);
                            await _objectTypeService.FireCreateEventAsync(context, remoteFile);

                            result.Add(new ChatMessage
                            {
                                Role = ChatRole.Assistant,
                                ContentType = part.InlineData.MimeType,
                                RemoteFileId = remoteFile.Id,
                                ThoughtSignature = part.ThoughtSignature,
                            });

                            assistantMessage = null;

                            _logger.LogInformation("File generated: {ContentType} {Size}: {RemoteFileId}", remoteFile.ContentType, remoteFile.Size, remoteFile.Id);

                            await response.WriteAsync("event: document\n");
                            await response.WriteAsync($"data: {JsonConvert.SerializeObject(new { ContentType = part.InlineData.MimeType, part.InlineData.Data.Length, remoteFile.Id, remoteFile.AbsoluteUri, remoteFile.Name })}\n\n");
                            await response.Body.FlushAsync();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming completion from Gemini API");
            await WriteErrorToResponse(response, $"Gemini API error: {ex.Message}");
            throw;
        }

        return result.ToArray();
    }

    public override async Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Chat chat)
    {
        var client = await GetClientAsync(context);
        if (!client.IsSuccess)
        {
            return client.ConvertTo<CompletionResponse>();
        }

        var request = await CreateRequestAsync(context, chat);
        if (!request.IsSuccess)
        {
            return request.ConvertTo<CompletionResponse>();
        }

        var assistant = chat.Assistant;

        try
        {
            var model = BuildModel(client.Value, assistant);
            var geminiResponse = await model.GenerateContentAsync(request.Value);

            var responseText = geminiResponse.Text;

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
            _logger.LogError(ex, "Error getting completion from Gemini API");
            return Result.Error<CompletionResponse>($"Gemini API error: {ex.Message}");
        }
    }

    private GenerativeModel BuildModel(GoogleAi client, Assistant assistant)
    {
        GenerationConfig config = null;
        
        // TODO: client code doesn't seem to support thinking level yet (just budget)
        // if (assistant.ModelProperties?.ThinkingLevel != null)
        // {
        //     config ??= new GenerationConfig();
        //     config.ThinkingConfig = new ThinkingConfig();
        // }

        return client.CreateGenerativeModel(GetModel(assistant), config);
    }

    private async Task<Result<GenerateContentRequest>> CreateRequestAsync(IEntityContext context, Chat chat)
    {
        var messages = chat.Messages;
        // var systemPrompt = string.Join("\n", messages.Where(x => x.Role == ChatRole.System).Select(x => x.Text));
        var request = new GenerateContentRequest
        {
            // SystemInstruction = !string.IsNullOrEmpty(systemPrompt) ? new Content(systemPrompt, Roles.System) : null,
        };

        var canProduceImage = CanModelProduceImages(chat.Assistant);
        if (canProduceImage)
        {
            request.GenerationConfig = new GenerationConfig
            {
                ResponseModalities =
                [
                    Modality.IMAGE,
                    Modality.TEXT
                ]
            };
        }

        var error = await buildContentAsync(request.Contents);
        if (error != null) Result<GenerateContentRequest>.Error(error);

        return Result.Success(request);

        async Task<string> buildContentAsync(List<Content> contents)
        {
            Content content = null;

            foreach (var message in messages)
            {
                var role = message.Role switch
                {
                    ChatRole.User => Roles.User,
                    ChatRole.Assistant => Roles.Model,
                    ChatRole.Tool => Roles.Function,
                    _ => Roles.User,
                };

                if (content == null || content.Role != role)
                {
                    content = new Content
                    {
                        Role = role,
                    };

                    contents.Add(content);
                }

                if (message.RemoteFileId.HasValue)
                {
                    // TODO: load file
                    var remoteFile = await _connection.Filter<PI.Shared.Models.Files.RemoteFile>()
                        .Eq(x => x.AccountId, context.AccountId)
                        .Eq(x => x.Id, message.RemoteFileId)
                        .Ne(x => x.IsActive, false)
                        .FirstOrDefaultAsync();

                    if (remoteFile == null || !(remoteFile.AllowAnonymousDownload || remoteFile.RBAC.Can(context, RemoteFilePermission.Read)))
                    {
                        return "File not found or permission denied";
                    }

                    var stream = await _remoteFileService.GetStreamAsync(context, remoteFile);
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    var base64 = Convert.ToBase64String(memoryStream.ToArray());
                    content.AddInlineData(base64, message.ContentType ?? remoteFile.ContentType);
                }
                else
                {
                    content.AddText(message.Text);
                }
                
                if (message.ThoughtSignature != null)
                {
                    content.Parts.First().ThoughtSignature = message.ThoughtSignature;
                }
            }

            return null;
        }
    }
    
    private async Task<Result<string>> GetGeminiApiKeyAsync(IEntityContext context)
    {
        var integration = await _connection.Filter<GeminiIntegrationConfiguration>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.AccountId.Value)
            .Eq(x => x.IntegrationId, IntegrationIds.Gemini)
            .FirstOrDefaultAsync();

        if (integration == null) return Result.Error<string>("Gemini integration not configured for account");

        var apiKey = await _dataProtectionService.UnprotectAsync(
            context,
            new MicrosoftDataProtectionConfig
            {
                Purpose = GeminiIntegrationConfiguration.ProtectionKey,
            },
            integration.APIKey
        );

        return Result<string>.Success(apiKey);
    }
}

public static class GeminiAssistantProviderExtensions
{
    public static IServiceCollection AddHttpInterceptorToGeminiProvider(this IServiceCollection services)
    {
        services.AddTransient<HttpClientLoggingHandler>();
        services.AddHttpClient<GeminiAssistantProvider>("Gemini")
            .AddDefaultLogger()
            .AddHttpMessageHandler<HttpClientLoggingHandler>();

        return services;
    }
}