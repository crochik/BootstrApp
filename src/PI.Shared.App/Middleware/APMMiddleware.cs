// using System;
// using System.Threading.Tasks;
// using Elastic.Apm;
// using Elastic.Apm.Api;
// using Microsoft.AspNetCore.Http;
// using Microsoft.Extensions.Logging;
// using PI.Shared.Models;
//
// namespace PI.Shared.Middleware
// {
//     public class APMMiddleware
//     {
//         private readonly RequestDelegate _next;
//
//         public APMMiddleware(RequestDelegate next)
//         {
//             _next = next;
//         }
//
//         public async Task InvokeAsync(HttpContext context, ILogger<APMMiddleware> logger)
//         {
//             ITransaction transaction = Agent.Tracer.CurrentTransaction;
//             if (transaction == null)
//             {
//                 await _next.Invoke(context);
//                 return;
//             }
//
//             if (context.GetContextWithActor()?.Actor is not AbstractAPIActor actor)
//             {
//                 // non-authorized?
//                 // ...
//                 await _next.Invoke(context);
//                 return;
//             }
//
//             transaction.Context.User = new Elastic.Apm.Api.User
//             {
//                 Id = actor.UserId?.ToString(),
//                 // UserName = 
//                 // Email = 
//             };
//
//             transaction.SetLabel("accountId", actor.AccountId.ToString());
//             transaction.SetLabel("clientId", actor.ClientId);
//             if (!string.IsNullOrEmpty(actor.TokenId)) transaction.SetLabel("jti", actor.TokenId);
//
//             try
//             {
//                 await _next.Invoke(context);
//                 transaction.Result = context.Response.StatusCode.ToString();
//             }
//             catch (Exception ex)
//             {
//                 transaction.CaptureException(ex);
//                 throw;
//             }
//         }
//     }
// }