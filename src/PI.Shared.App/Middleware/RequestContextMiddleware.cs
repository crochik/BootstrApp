using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace PI.Shared.Middleware;

public class RequestContextMiddleware
{
    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<RequestContextMiddleware> logger, IWebHostEnvironment env)
    {
        if (context.GetContextWithActor()?.Actor is not AbstractAPIActor actor)
        {
            await _next.Invoke(context);
            return;
        }

        var scope = new Dictionary<string, object>
        {
            {"AccountId", actor.AccountId},
            {"ClientId", actor.ClientId}
        };

        if (actor.UserId.HasValue) scope.Add("UserId", actor.UserId);

        scope.TryAdd("RequestId", actor.RequestId); // to avoid duplicating 
        if (!string.IsNullOrEmpty(actor.TokenId)) scope.Add("Jti", actor.TokenId);

        using (logger.BeginScope(scope))
        {
            await _next.Invoke(context);
        }
    }
}