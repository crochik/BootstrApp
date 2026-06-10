using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Webhook.Integrations.Core.Configuration;

namespace Webhook.Integrations.Core.Auth;

/// <summary>
/// Guards every route under the integration's configured prefix with an API key. The
/// key is presented as <c>X-Api-Key</c> or <c>Authorization: Bearer &lt;key&gt;</c>;
/// both are accepted and compared in constant time. When no keys are configured the
/// gate is open (local experiments only). Routes outside the prefix (e.g.
/// <c>/health</c>) pass through.
/// </summary>
public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<IntegrationOptions> _options;

    public ApiKeyMiddleware(RequestDelegate next, IOptionsMonitor<IntegrationOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var options = _options.CurrentValue;
        if (!context.Request.Path.StartsWithSegments(options.RoutePrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (options.ApiKeys.Count == 0 || IsAuthorized(context.Request, options.ApiKeys))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\":\"invalid or missing API key\"}");
    }

    private static bool IsAuthorized(HttpRequest request, IReadOnlyCollection<string> keys)
    {
        var presented = ExtractKey(request);
        if (string.IsNullOrEmpty(presented))
        {
            return false;
        }

        // Constant-time over every configured key so timing can't reveal a match.
        var match = false;
        foreach (var key in keys)
        {
            match |= FixedTimeEquals(presented, key);
        }

        return match;
    }

    private static string? ExtractKey(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Api-Key", out var apiKey) && !string.IsNullOrEmpty(apiKey))
        {
            return apiKey.ToString();
        }

        var auth = request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";
        if (auth.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return auth[bearer.Length..].Trim();
        }

        return null;
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));
}
