using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.LangChain.Models;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Requests;
using Services;

namespace PI.LangChain.Controllers;

[Route("/langchain/v1/[controller]")]
public class AssistantController : APIController
{
    private readonly MongoConnection _connection;
    private readonly AssistantCompletionService _completionService;

    public AssistantController(MongoConnection connection, AssistantCompletionService completionService)
    {
        _connection = connection;
        _completionService = completionService;
    }
    
    [Authorize("admin")]
    [HttpGet("/langchain/v1/[controller]({assistantId})/DataForm")]
    [HttpGet("DataForm")]
    public async Task<Form> GetForm1Async([FromRoute] Guid assistantId, [FromQuery] Guid? id)
    {
        id ??= assistantId;

        var assistant = await _connection.Filter<Assistant>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, id.Value)
            .FirstOrDefaultAsync();

        return new Form
        {
            Title = assistant.Description ?? assistant.Name,
            Name = assistant.Name,
            Fields = fields().ToArray(),
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Complete",
                    Action = "Complete"
                }
            }
        };

        IEnumerable<FormField> fields()
        {
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

            if (assistant.Inputs?.Count > 0)
            {
                // ????
                foreach (var kvp in assistant.Inputs)
                {
                    yield return new TextField
                    {
                        Name = $"Inputs|{kvp.Key}",
                        Label = kvp.Key,
                        DefaultValue = kvp.Value,
                        IsRequired = true,
                        TextFieldOptions = new TextFieldOptions
                        {
                            Multline = true,
                        }
                    };
                }
            }
        }
    }

    [Authorize("admin")]
    [HttpPost("/langchain/v1/[controller]({id})/DataForm")]
    [HttpPost("DataForm")]
    public async Task<DataFormActionResponse> GetRequestDataFormAsync([FromRoute] Guid assistantId, [FromQuery] Guid? id, [FromBody] DataFormActionRequest request)
    {
        id ??= assistantId;

        var assistant = await _connection.Filter<Assistant>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, id.Value)
            .FirstOrDefaultAsync();

        if (!request.TryGetStrParam("UserMessage", out var userMessage))
        {
            return DataFormActionResponse.Error(request, "Missing required UserMessage parameter");
        }

        var inputs = new Dictionary<string, object>();
        if (assistant.Inputs?.Count > 0)
        {
            // inputs...
            // ...
        }

        var result = await _completionService.GetCompletionAsync(Context, id.Value, userMessage, inputs);
        if (!result.IsSuccess)
        {
            return DataFormActionResponse.Error(request, result.Status);
        }

        await _connection.InsertAsync(result.Value.Chat);

        return new DataFormActionResponse(request)
        {
            Success = true,
            NextUrl = $"dataform:/langchain/v1/Chat({result.Value.Chat.Id})"
            // NextUrl = $"page:/GenAIContent?id={result.Value.Chat.Id}"
        };
    }
}