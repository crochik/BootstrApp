// using System;
// using System.Threading.Tasks;
// using Crochik.Mongo;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using PI.LangChain.Models;
// using PI.Shared.Controllers;
//
// namespace Controllers;
//
// [Route("/api/v1/[controller]")]
// [Authorize("default")]
// public class AssistantController : APIController
// {
//     private readonly MongoConnection _connection;
//
//     public AssistantController(MongoConnection connection)
//     {
//         _connection = connection;
//     }
//     
//     [Authorize("admin")]
//     [DefaultContractJsonFilter]
//     [HttpGet("/api/v1/[controller]({id})")]
//     public async Task<Assistant> GetAsync([FromRoute] Guid id)
//     {
//         var assistant = await _connection.Filter<Assistant>()
//             .Eq(x => x.AccountId, Context.AccountId)
//             .Eq(x => x.Id, id)
//             .FirstOrDefaultAsync();
//
//         return assistant;
//     }
// }