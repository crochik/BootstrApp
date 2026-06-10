using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PI.LangChain.Models;
using PI.Shared.Models;
using ChatMessage = PI.LangChain.Models.ChatMessage;

namespace Services;

public interface IAssistantProvider
{
    Guid IntegrationId { get; }
    
    /// <summary>
    /// Initiate chat
    /// </summary>
    Task<Result<CompletionResponse>>  GetCompletionAsync(IEntityContext context, Chat chat);

    /// <summary>
    /// Continue conversation (potentially using different assistant)
    /// </summary>
    Task<Result<CompletionResponse>> GetCompletionAsync(IEntityContext context, Chat chat, Assistant assistant, IEnumerable<ChatMessage> additionalMessages, IDictionary<string, object> objectContext);

    /// <summary>
    /// Stream response 
    /// </summary>
    Task<ChatMessage[]> GetCompletionStreamAsync(IEntityContext context, HttpResponse response, Chat chat);
}

public class CompletionResponse
{
    public string ContentType { get; set; }
    public string ResponseFormat { get; set; }
    public string Response { get; set; }
    
    public Chat Chat { get; set; }
}