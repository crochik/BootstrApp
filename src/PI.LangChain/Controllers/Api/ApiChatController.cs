using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.LangChain.Models;
using PI.Shared.Exceptions;
using PI.Shared.Services;
using Services;

namespace PI.LangChain.Controllers;

[Authorize("rest")]
[Route("/langchain/api/Chat")]
public class ApiChatController : AbstractChatController
{
    public ApiChatController(ILogger<ApiChatController> logger, MongoConnection connection, AssistantCompletionService assistantService, ObjectTypeService objectTypeService) : base(logger, connection, objectTypeService, assistantService)
    {
    }

    [HttpGet("/langchain/api/Chat({chatId})/Stream")]
    public async Task StreamChat([FromRoute] Guid chatId, [FromQuery] string userMessage, [FromQuery] Guid? promptId, [FromQuery] Guid? remoteFileId)
    {
        var chat = await _connection.Filter<Chat>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, Context.UserId.Value)
            .Eq(x => x.Id, chatId)
            .FirstOrDefaultAsync();

        if (chat == null) throw NotFoundException.New<Chat>(chatId);
        
        await StreamChatAsync(Context, chat, remoteFileId: remoteFileId, userMessage: userMessage, promptId: promptId);
    }
}