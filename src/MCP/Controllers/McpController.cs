using Microsoft.AspNetCore.Mvc;
using McpServer.Models;
using McpServer.Services;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace McpServer.Controllers;

[ApiController]
[Route("mcp")]
public class McpController(
    ILogger<McpController> logger,
    IMcpProtocolHandler protocolHandler,
    IOidcConfigurationService oidcConfigService)
    : ControllerBase
{
    protected IContextWithActor Context => HttpContext.GetContextWithActor();

    [HttpDelete]
    [Consumes("application/problem+json", "application/json")]
    public async Task<IActionResult> DeleteSse([FromBody] string? body)
    {
        // await LogRequest();

        return NoContent();
    }

    // MCP Streamable HTTP allows servers to opt out of the server-initiated SSE stream
    // by responding 405 to GET. Clients that probe will then rely on the response-mode
    // SSE returned from POST instead of retrying GET.
    [HttpGet]
    public IActionResult RejectGetSse()
    {
        Response.Headers["Allow"] = "POST, DELETE";
        return StatusCode(StatusCodes.Status405MethodNotAllowed);
    }

    /// <summary>
    /// MCP SSE endpoint - handles all JSON-RPC messages over HTTP POST
    /// This is the main entry point for MCP protocol communication
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleSsePost([FromBody] McpRequest request)
    {
        LogRequestMetadata();

        logger.LogInformation("SSE POST received - Method: {Method}, Id: {Id}", request.Method, request.Id);

        try
        {
            // Explicitly trigger authentication (needed when [Authorize] is not present)
            var authenticateResult = await HttpContext.AuthenticateAsync();
            if (authenticateResult.Succeeded)
            {
                HttpContext.User = authenticateResult.Principal;
            }

            // Extract or generate session ID
            var sessionId = Request.Headers["Mcp-Session-Id"].FirstOrDefault();
            if (string.IsNullOrEmpty(sessionId))
            {
                // First request - generate new session
                sessionId = Guid.NewGuid().ToString();
                logger.LogInformation("New SSE session created: {SessionId}", sessionId);
            }
            else
            {
                logger.LogInformation("Existing SSE session: {SessionId}", sessionId);
            }

            // Always include session ID in response
            Response.Headers.Add("Mcp-Session-Id", sessionId);

            // MCP Streamable HTTP: if the client opted in via Accept: text/event-stream,
            // serve tools/call responses as a single-stream SSE so we can also push
            // notifications/tools/list_changed when a tool mutates the catalog. All other
            // methods stay on the JSON path even under SSE Accept (no benefit to streaming
            // a single message).
            var wantsSse = Request.Headers.Accept
                .ToString()
                .Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);

            // Coarse-grained scope gate: any tools/* method needs mcp:tools, any
            // resources/* method needs mcp:resources. Runs before the per-tool
            // RequiresAuthentication check so unscoped callers can't even list.
            // initialize/ping/notifications/* are unaffected (no scope mapped).
            var requiredScope = RequiredScopeForMethod(request.Method);
            if (requiredScope != null && !PrincipalHasScope(User, requiredScope))
            {
                var hasAuth = User.Identity?.IsAuthenticated == true;
                var scopeResponse = new McpResponse
                {
                    Jsonrpc = "2.0",
                    Error = new McpError
                    {
                        Code = -32001,
                        Message = hasAuth
                            ? $"Insufficient scope: '{requiredScope}' is required for '{request.Method}'."
                            : $"Authentication required: scope '{requiredScope}' is required for '{request.Method}'."
                    },
                    Id = request.Id,
                    RequiresAuthChallenge = true
                };

                await HttpContext.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme);
                if (hasAuth)
                {
                    // RFC 6750 §3 — when the token is valid but lacks scope, surface
                    // insufficient_scope so MCP clients prompt for the missing scope
                    // on the next sign-in instead of silently re-presenting the same token.
                    Response.Headers["WWW-Authenticate"] =
                        $"Bearer error=\"insufficient_scope\", scope=\"{requiredScope}\"";
                }

                if (wantsSse && request.Method == "tools/call")
                {
                    await WriteSseResponseAsync(scopeResponse);
                }
                else
                {
                    Response.Headers.TryAdd("Content-Type", "application/json");
                    var json = JsonSerializer.Serialize(scopeResponse, SseJsonOptions);
                    await Response.WriteAsync(json);
                }
                return new EmptyResult();
            }

            if (wantsSse && request.Method == "tools/call")
            {
                var sseContext = User.Identity?.IsAuthenticated == true ? Context : null;
                var sseResponse = await protocolHandler.HandleRequestAsync(sseContext, request);
                if (IsAuthRequired(sseResponse))
                {
                    await HttpContext.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme);
                }

                await WriteSseResponseAsync(sseResponse);
                return new EmptyResult();
            }

            Response.Headers.Add("Content-Type", "application/json");

            // Handle special initialize method
            if (request.Method == "initialize")
            {
                var initializeResult = new InitializeResult
                {
                    ProtocolVersion = "2025-03-26", // MCP protocol version
                    Capabilities = new Dictionary<string, object>
                    {
                        { "tools", new Dictionary<string, object> { { "listChanged", true } } },
                        { "resources", new Dictionary<string, object> { { "listChanged", true } } },
                    },
                    ServerInfo = new ServerInfo
                    {
                        Name = "MCP OAuth Server",
                        Version = "1.0.0"
                    }
                };

                var jsonRpcResponse = new JsonRpcResponse<InitializeResult>
                {
                    Id = request.Id ?? 0,
                    Result = initializeResult
                };

                logger.LogInformation("Initialize response sent with session: {SessionId}", sessionId);

                // Return the object directly, let ASP.NET serialize it
                return Ok(jsonRpcResponse);
            }

            // Handle notifications (no response needed)
            if (request.Method?.StartsWith("notifications/") == true)
            {
                logger.LogInformation("Notification received: {Method}", request.Method);
                // Notifications don't require a response, return 202 Accepted
                return StatusCode(202);
            }

            var context = User.Identity?.IsAuthenticated == true ? Context : null;

            // Handle all other MCP protocol methods using the protocol handler
            logger.LogInformation("Processing MCP request via protocol handler: {Method}", request.Method);
            var response = await protocolHandler.HandleRequestAsync(context, request);

            if (IsAuthRequired(response))
            {
                await HttpContext.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme);
                var json = JsonSerializer.Serialize(response, SseJsonOptions);
                await Response.WriteAsync(json);
                return new EmptyResult();
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling SSE POST: {Method}", request.Method);
            return StatusCode(500, new McpResponse
            {
                Jsonrpc = "2.0",
                Error = new McpError
                {
                    Code = -32603,
                    Message = $"Internal error: {ex.Message}"
                },
                Id = request.Id
            });
        }
    }

    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Tool input schemas advertise enums as strings; mirror that on the wire.
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
    };

    private static bool IsAuthRequired(McpResponse response) => response.RequiresAuthChallenge;

    private static string? RequiredScopeForMethod(string? method)
    {
        if (method == null) return null;
        if (method.StartsWith("tools/", StringComparison.Ordinal)) return "mcp:tools";
        if (method.StartsWith("resources/", StringComparison.Ordinal)) return "mcp:resources";
        return null;
    }

    private static bool PrincipalHasScope(ClaimsPrincipal? principal, string requiredScope)
    {
        if (principal?.Identity?.IsAuthenticated != true) return false;

        // OAuth 2.0 access tokens carry scopes in a 'scope' claim (space-delimited per
        // RFC 6749 §3.3); some IDPs (notably AAD) emit 'scp' instead. Accept either,
        // and tolerate both single space-delimited values and multiple single-value claims.
        foreach (var claim in principal.FindAll("scope"))
        {
            foreach (var value in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(value, requiredScope, StringComparison.Ordinal)) return true;
            }
        }
        foreach (var claim in principal.FindAll("scp"))
        {
            foreach (var value in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(value, requiredScope, StringComparison.Ordinal)) return true;
            }
        }
        return false;
    }

    private async Task WriteSseResponseAsync(McpResponse response)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";

        await WriteSseEventAsync(JsonSerializer.Serialize(response, SseJsonOptions));

        if (response.TriggersListChanged)
        {
            var notification = new McpNotification { Method = "notifications/tools/list_changed" };
            await WriteSseEventAsync(JsonSerializer.Serialize(notification, SseJsonOptions));
        }
    }

    private async Task WriteSseEventAsync(string data)
    {
        var bytes = Encoding.UTF8.GetBytes($"event: message\ndata: {data}\n\n");
        await Response.Body.WriteAsync(bytes);
        await Response.Body.FlushAsync();
    }

    // [HttpGet("health")]
    // public async Task<IActionResult> Health([FromBody] object obj)
    // {
    //     await LogRequest();
    //     _logger.LogInformation("Body: {body}", obj.ToString());
    //
    //     return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    // }

    [HttpPost("~/register")]
    public async Task<IActionResult> Register([FromBody] ClientRegistrationRequest request)
    {
        var response = await oidcConfigService.RegisterClientAsync(Request, request);
        if (response.IsError)
        {
            return BadRequest(response.Status switch
            {
                "invalid_client_metadata" => new
                {
                    error = "invalid_client_metadata",
                    error_description = "Required fields 'redirect_uris' and 'client_name' must be provided."
                },
                "invalid_redirect_uri" => new
                {
                    error = "invalid_redirect_uri",
                    error_description = "Redirect URIs must be valid and use HTTPS, except for loopback/native apps."
                },
                _ => new
                {
                    error = response.Status,
                    error_description = response.Status,
                }
            });
        }

        // 6. Return 201 Created
        return Created(response.Value.RegistrationClientUri, response.Value);
    }

    // TODO: it does fail when enabled...probably because it is missing the register
    // ...
    /// <summary>
    /// OAuth 2.0 Authorization Server Metadata endpoint (RFC 8414) with MCP extensions
    /// Returns metadata about the OAuth 2.0 authorization server
    /// </summary>
    [HttpGet("~/.well-known/oauth-authorization-server")]
    public async Task<IActionResult> OAuthMetadata()
    {
        // Add MCP-Protocol-Version header as recommended by MCP spec
        Response.Headers["MCP-Protocol-Version"] = "2025-03-26";

        var oidcMetadata = await oidcConfigService.GetOAuthMetadataAsync(Request);

        return Ok(oidcMetadata);
    }

    /// <summary>
    /// OAuth 2.0 Protected Resource Metadata endpoint (RFC 9728).
    /// Served at both the bare path and the path-suffixed form per RFC 9728 §3.1,
    /// so MCP clients that look up metadata by the resource path (`/mcp`) succeed.
    /// </summary>
    [HttpGet("~/.well-known/oauth-protected-resource")]
    [HttpGet("~/.well-known/oauth-protected-resource/mcp")]
    public async Task<IActionResult> OAuthProtectedResourceMetadata()
    {
        Response.Headers["MCP-Protocol-Version"] = "2025-03-26";
        
        var metadata = await oidcConfigService.OAuthProtectedResourceMetadata(Request);
        if (metadata.IsError)
        {
            logger.LogError("Error getting {Path}: {Status}", Request.Path, metadata.Status);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        
        logger.LogInformation("OAuth protected resource metadata served for {Resource}", metadata.Value.Resource);

        return Ok(metadata.Value);
    }

    private void LogRequestMetadata()
    {
        var log = new StringBuilder();
        log.AppendLine("--- HTTP REQUEST START ---");
        log.AppendLine($"Method: {Request.Method}");
        log.AppendLine($"Path: {Request.Path}{Request.QueryString}");
        // log.AppendLine("Headers:");
        //
        // foreach (var header in Request.Headers)
        // {
        //     // You might want to redact sensitive headers like Authorization
        //     log.AppendLine($"  {header.Key}: {header.Value}");
        // }

        logger.LogInformation(log.ToString());
    }

    private async Task LogRequest()
    {
        LogRequestMetadata();

        // The request body stream is forward-only, so we need to enable 
        // buffering to allow the downstream model binder to read it.
        // Request.EnableBuffering();

        // Leave the stream open for the downstream model binder
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, true, 1024, true);
        var body = await reader.ReadToEndAsync();

        if (!string.IsNullOrWhiteSpace(body))
        {
            logger.LogInformation("Body:\n{RequestBody}", body);
        }

        // **CRITICAL**: Reset the stream's position to 0 so the model binder 
        // can read it again later in the pipeline.
        // Request.Body.Position = 0;
    }


    /// <summary>
    /// Defines the JSON-RPC standard response structure for a successful result.
    /// </summary>
    /// <typeparam name="T">The type of the result payload (e.g., InitializeResult).</typeparam>
    public class JsonRpcResponse<T>
    {
        [JsonPropertyName("jsonrpc")] public string JsonRpc { get; } = "2.0";

        [JsonPropertyName("result")] public T Result { get; set; }

        [JsonPropertyName("id")] public int Id { get; set; } // Matches the ID from the incoming request
    }

    /// <summary>
    /// The expected payload for a successful 'initialize' response.
    /// </summary>
    public class InitializeResult
    {
        [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; set; }

        [JsonPropertyName("capabilities")] public Dictionary<string, object> Capabilities { get; set; } // Server capabilities

        [JsonPropertyName("serverInfo")] public ServerInfo ServerInfo { get; set; }
    }

    /// <summary>
    /// Server information for initialize response
    /// </summary>
    public class ServerInfo
    {
        [JsonPropertyName("name")] public string Name { get; set; }

        [JsonPropertyName("version")] public string Version { get; set; }
    }
}