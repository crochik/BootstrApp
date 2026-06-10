using McpServer.Models;
using McpServer.Services;
using McpServer.Tools.Attributes;
using Microsoft.AspNetCore.Http;

namespace McpServer.Tools;

/// <summary>
/// Façade implementation of the Anthropic-connector-style <c>authenticate</c> /
/// <c>complete_authentication</c> tool pair. The MCP server is stateless
/// JWT-bearer: the client (e.g. Claude Code) drives the OAuth flow itself via
/// the RFC 9728 protected-resource metadata. These tools exist so the assistant
/// has a discoverable surface to guide the user; they do not mint tokens.
/// </summary>
public sealed class AuthenticationTools
{
    private readonly IOidcConfigurationService _oidc;
    private readonly IHttpContextAccessor _http;

    public AuthenticationTools(IOidcConfigurationService oidc, IHttpContextAccessor http)
    {
        _oidc = oidc;
        _http = http;
    }

    [McpTool(
        Name = "authenticate",
        Description =
            "Begin authenticating to this MCP server. Returns a URL the user should open " +
            "in their browser. After they finish signing in, call `complete_authentication`.",
        RequiresAuthentication = false,
        ExamplePrompts = new[]
        {
            "authenticate",
            "log in",
            "connect this server"
        })]
    public async Task<object> Authenticate()
    {
        var oidc = await _oidc.GetConfigurationAsync()
            ?? throw new McpToolException("OIDC is not configured on this server.");

        var req = _http.HttpContext?.Request
            ?? throw new McpToolException("No active HTTP request — cannot build server URLs.");
        var baseUrl = $"{req.Scheme}://{req.Host}";

        return new
        {
            // RFC 9728 — the MCP client should fetch this and run the standard
            // OAuth flow (PKCE, dynamic client registration, authorize, token).
            protectedResourceMetadata = $"{baseUrl}/.well-known/oauth-protected-resource",

            // Fallback for humans: opening this URL won't complete the flow on its
            // own (no client_id / redirect_uri / PKCE), but it shows the IDP that
            // backs this server.
            authorizationServer = oidc.Issuer,
            authorizationEndpoint = oidc.AuthorizationEndpoint,

            instructions =
                "If your MCP client supports MCP OAuth (RFC 9728), it should fetch " +
                "`protectedResourceMetadata` and run the flow automatically. Otherwise " +
                "open `authorizationServer` in a browser and sign in. When done, call " +
                "`complete_authentication`."
        };
    }

    [McpTool(
        Name = "complete_authentication",
        Description =
            "Acknowledge that the user has finished signing in. Subsequent tool calls " +
            "will use the bearer token your MCP client now holds.",
        RequiresAuthentication = false)]
    public string CompleteAuthentication()
    {
        // The server is stateless — there's nothing to "complete." This exists so the
        // assistant has a tool to call after the user finishes the browser flow,
        // matching the connector convention.
        return
            "Authentication acknowledged. Try a tool that requires authentication " +
            "(e.g. anything other than `authenticate` / `complete_authentication`) to " +
            "verify your session is active.";
    }
}
