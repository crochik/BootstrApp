using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Services;
using Services;

namespace PI.LangChain.Controllers;

[Authorize("rest")]
[Route("/langchain/api/Assistant")]
public class ApiAssistantController : AbstractChatController
{
    public ApiAssistantController(ILogger<ApiAssistantController> logger, MongoConnection connection, AssistantCompletionService assistantService, ObjectTypeService objectTypeService) : base(logger, connection, objectTypeService, assistantService)
    {
    }

    [HttpGet("/langchain/api/Assistant({assistantId})/Stream")]
    public async Task StreamChat([FromRoute] Guid assistantId, [FromQuery] string userMessage, [FromQuery] Guid? promptId, [FromQuery] Guid? remoteFileId)
    {
        var inputs = new Dictionary<string, object>();
        // TODO: parse query parameters
        // ...

        var chat = await _assistantService.AssistantService.CreateChatAsync(Context, assistantId, null, inputs);

        await StreamChatAsync(Context, chat, remoteFileId: remoteFileId, userMessage: userMessage, promptId: promptId);
    }
}