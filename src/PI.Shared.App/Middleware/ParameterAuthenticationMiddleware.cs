using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace PI.Shared.Middleware;

public class ParameterAuthenticationMiddleware
{
    private readonly RequestDelegate next;

    public ParameterAuthenticationMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey("Authorization"))
        {
            var accessToken = default(string);
            if (context.Request.Query.ContainsKey("access_token"))
            {
                accessToken = context.Request.Query["access_token"];
            }
            else if (context.Request.HasFormContentType && context.Request.Form.ContainsKey("access_token"))
            {
                accessToken = context.Request.Form["access_token"];
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                context.Request.Headers["Authorization"] = $"Bearer {accessToken}";
            }
        }

        await next(context);
    }
}