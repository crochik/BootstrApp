using McpServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace McpServer.Middleware;

public class TokenValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenValidationMiddleware> _logger;

    public TokenValidationMiddleware(RequestDelegate next, ILogger<TokenValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuthenticationService authService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip token validation for authentication endpoint and SSE initialization
        if (path.Contains("/mcp") || path.Contains("/health"))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();

            if (authService.ValidateToken(token))
            {
                var username = authService.GetUsernameFromToken(token);
                if (username != null)
                {
                    context.Items["Username"] = username;
                    context.Items["Token"] = token;
                }
            }
        }

        await _next(context);
    }
}
