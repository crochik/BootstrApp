using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PI.Shared.Models;

namespace PI.Shared.Middleware;

public class ActorMiddleware
{
    private readonly RequestDelegate _next;

    public ActorMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext httpContext)
    {
        var context = httpContext.GetContextWithActor();
        if (context != null)
        {
            Actor.Current = context.Actor();
        }

        return _next.Invoke(httpContext);
    }
}