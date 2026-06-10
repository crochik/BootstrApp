// using Microsoft.AspNetCore.Mvc;
// using McpServer.Models;
// using McpServer.Services;
// using System.Text;
// using System.Text.Json;
// using System.Text.Json.Serialization;
// using Microsoft.AspNetCore.Authentication;
// using Microsoft.AspNetCore.Http;
// using Microsoft.Extensions.Logging;
// using PI.Shared.Models;
//
// namespace McpServer.Controllers;
//
// [ApiController]
// [Route("mcp")]
// public class McpController : ControllerBase
// {
//     protected IContextWithActor Context => HttpContext.GetContextWithActor();
//     
//     public static Dictionary<string, ClientRegistrationRequest> ClientStore = new();
//
//     private readonly ILogger<McpController> _logger;
//     private readonly IMcpProtocolHandler _protocolHandler;
//     private readonly IOidcConfigurationService _oidcConfigService;
//     private readonly IMcpSessionStore _sessionStore;
//
//     public McpController(
//         ILogger<McpController> logger,
//         IMcpProtocolHandler protocolHandler,
//         IOidcConfigurationService oidcConfigService,
//         IMcpSessionStore sessionStore)
//     {
//         _logger = logger;
//         _protocolHandler = protocolHandler;
//         _oidcConfigService = oidcConfigService;
//         _sessionStore = sessionStore;
//     }
//
//     [HttpDelete]
//     [Consumes("application/problem+json", "application/json")]
//     public async Task<IActionResult> DeleteSse([FromBody] string? body)
//     {
//         // await LogRequest();
//
//         return NoContent();
//     }
//
//     // MCP Streamable HTTP allows servers to opt out of the server-initiated SSE stream
//     // by responding 405 to GET. Clients that probe will then rely on the response-mode
//     // SSE returned from POST instead of retrying GET.
//     [HttpGet]
//     public IActionResult RejectGetSse()
//     {
//         Response.Headers["Allow"] = "POST, DELETE";
//         return StatusCode(StatusCodes.Status405MethodNotAllowed);
//     }
//
//     /// <summary>
//     /// MCP SSE endpoint - handles all JSON-RPC messages over HTTP POST
//     /// This is the main entry point for MCP protocol communication
//     /// </summary>
//     [HttpPost]
//     public async Task<IActionResult> HandleSsePost([FromBody] McpRequest request)
//     {
//         LogRequestMetadata();
//
//         _logger.LogInformation("SSE POST received - Method: {Method}, Id: {Id}", request.Method, request.Id);
//
//         try
//         {
//             // Explicitly trigger authentication (needed when [Authorize] is not present)
//             var authenticateResult = await HttpContext.AuthenticateAsync();
//             if (authenticateResult.Succeeded)
//             {
//                 HttpContext.User = authenticateResult.Principal;
//             }
//
//             // Extract or generate session ID
//             var sessionId = Request.Headers["Mcp-Session-Id"].FirstOrDefault();
//             if (string.IsNullOrEmpty(sessionId))
//             {
//                 // First request - generate new session
//                 sessionId = Guid.NewGuid().ToString();
//                 _logger.LogInformation("New SSE session created: {SessionId}", sessionId);
//             }
//             else
//             {
//                 _logger.LogInformation("Existing SSE session: {SessionId}", sessionId);
//             }
//
//             // Always include session ID in response
//             Response.Headers.Add("Mcp-Session-Id", sessionId);
//
//             // MCP Streamable HTTP: if the client opted in via Accept: text/event-stream,
//             // serve tools/call responses as a single-stream SSE so we can also push
//             // notifications/tools/list_changed when a tool mutates the catalog. All other
//             // methods stay on the JSON path even under SSE Accept (no benefit to streaming
//             // a single message).
//             var wantsSse = Request.Headers.Accept
//                 .ToString()
//                 .Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);
//
//             if (wantsSse && request.Method == "tools/call")
//             {
//                 var sseContext = User.Identity?.IsAuthenticated == true ? Context : null;
//                 var sseClient = _sessionStore.Get(sessionId);
//                 var sseResponse = await _protocolHandler.HandleRequestAsync(sseContext, request, sseClient);
//                 await WriteSseResponseAsync(sseResponse);
//                 return new EmptyResult();
//             }
//
//             Response.Headers.Add("Content-Type", "application/json");
//
//             // Handle special initialize method
//             if (request.Method == "initialize")
//             {
//                 _sessionStore.Set(sessionId, ParseClientInfo(request.Params));
//
//                 var initializeResult = new InitializeResult
//                 {
//                     ProtocolVersion = "2025-03-26", // MCP protocol version
//                     Capabilities = new Dictionary<string, object>
//                     {
//                         { "tools", new Dictionary<string, object> { { "listChanged", true } } },
//                     },
//                     ServerInfo = new ServerInfo
//                     {
//                         Name = "MCP OAuth Server",
//                         Version = "1.0.0"
//                     }
//                 };
//
//                 var jsonRpcResponse = new JsonRpcResponse<InitializeResult>
//                 {
//                     Id = request.Id ?? 0,
//                     Result = initializeResult
//                 };
//
//                 _logger.LogInformation("Initialize response sent with session: {SessionId}", sessionId);
//
//                 // Return the object directly, let ASP.NET serialize it
//                 return Ok(jsonRpcResponse);
//             }
//
//             // Handle notifications (no response needed)
//             if (request.Method?.StartsWith("notifications/") == true)
//             {
//                 _logger.LogInformation("Notification received: {Method}", request.Method);
//                 // Notifications don't require a response, return 202 Accepted
//                 return StatusCode(202);
//             }
//
//             var context = User.Identity?.IsAuthenticated == true ? Context : null;
//             var client = _sessionStore.Get(sessionId);
//
//             // Handle all other MCP protocol methods using the protocol handler
//             _logger.LogInformation("Processing MCP request via protocol handler: {Method}", request.Method);
//             var response = await _protocolHandler.HandleRequestAsync(context, request, client);
//
//             return Ok(response);
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Error handling SSE POST: {Method}", request.Method);
//             return StatusCode(500, new McpResponse
//             {
//                 Jsonrpc = "2.0",
//                 Error = new McpError
//                 {
//                     Code = -32603,
//                     Message = $"Internal error: {ex.Message}"
//                 },
//                 Id = request.Id
//             });
//         }
//     }
//
//     private static readonly JsonSerializerOptions SseJsonOptions = new()
//     {
//         PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
//         DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
//     };
//
//     /// <summary>
//     /// Parses an MCP <c>initialize</c> request's params for protocolVersion, clientInfo,
//     /// and the experimental <c>structuredContent</c> capability the client declared.
//     /// Tolerates missing/malformed fields — anything we can't read just stays null.
//     /// </summary>
//     private static McpClientInfo ParseClientInfo(object? rawParams)
//     {
//         if (rawParams is not JsonElement root || root.ValueKind != JsonValueKind.Object)
//             return new McpClientInfo();
//
//         string? protocolVersion = TryGetString(root, "protocolVersion");
//
//         string? clientName = null;
//         string? clientVersion = null;
//         if (root.TryGetProperty("clientInfo", out var ci) && ci.ValueKind == JsonValueKind.Object)
//         {
//             clientName = TryGetString(ci, "name");
//             clientVersion = TryGetString(ci, "version");
//         }
//
//         StructuredContentCapability? sc = null;
//         if (root.TryGetProperty("capabilities", out var caps)
//             && caps.ValueKind == JsonValueKind.Object
//             && caps.TryGetProperty("experimental", out var exp)
//             && exp.ValueKind == JsonValueKind.Object
//             && exp.TryGetProperty("structuredContent", out var scNode)
//             && scNode.ValueKind == JsonValueKind.Object)
//         {
//             bool supported = scNode.TryGetProperty("supported", out var sup)
//                              && sup.ValueKind == JsonValueKind.True;
//             bool includeContent = !scNode.TryGetProperty("includeContent", out var inc)
//                                   || inc.ValueKind != JsonValueKind.False;
//             sc = new StructuredContentCapability
//             {
//                 Supported = supported,
//                 IncludeContent = includeContent
//             };
//         }
//
//         return new McpClientInfo
//         {
//             ProtocolVersion = protocolVersion,
//             Name = clientName,
//             Version = clientVersion,
//             StructuredContent = sc
//         };
//     }
//
//     private static string? TryGetString(JsonElement element, string property) =>
//         element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
//             ? v.GetString()
//             : null;
//
//     private async Task WriteSseResponseAsync(McpResponse response)
//     {
//         Response.ContentType = "text/event-stream";
//         Response.Headers["Cache-Control"] = "no-cache";
//
//         await WriteSseEventAsync(JsonSerializer.Serialize(response, SseJsonOptions));
//
//         if (response.TriggersListChanged)
//         {
//             var notification = new McpNotification { Method = "notifications/tools/list_changed" };
//             await WriteSseEventAsync(JsonSerializer.Serialize(notification, SseJsonOptions));
//         }
//     }
//
//     private async Task WriteSseEventAsync(string data)
//     {
//         var bytes = Encoding.UTF8.GetBytes($"event: message\ndata: {data}\n\n");
//         await Response.Body.WriteAsync(bytes);
//         await Response.Body.FlushAsync();
//     }
//
//     // [HttpGet("health")]
//     // public async Task<IActionResult> Health([FromBody] object obj)
//     // {
//     //     await LogRequest();
//     //     _logger.LogInformation("Body: {body}", obj.ToString());
//     //
//     //     return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
//     // }
//
//     [HttpPost("~/register")]
//     public async Task<IActionResult> Register([FromBody] ClientRegistrationRequest request)
//     {
//         // await LogRequest();
//         // _logger.LogInformation("Body: {body}", JsonSerializer.Serialize(request));
//
//         // 1. Initial Access Token (IAT) Check (Security)
//         // DCR endpoints are typically protected by an Initial Access Token (IAT).
//         // If open registration is allowed, this step can be skipped.
//         // TODO: Enable IAT validation in production environments
//         // var authHeader = Request.Headers.Authorization.FirstOrDefault();
//         // if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
//         // {
//         //     // In a production app, you would validate the IAT against an active token store.
//         //     // For this example, we allow open registration for development/testing.
//         //     return Unauthorized();
//         // }
//
//         // 2. Input Validation (RFC 7591)
//         // Check for required fields and proper format.
//         if (request.RedirectUris.Length == 0 || string.IsNullOrEmpty(request.ClientName))
//         {
//             return BadRequest(new
//             {
//                 error = "invalid_client_metadata",
//                 error_description = "Required fields 'redirect_uris' and 'client_name' must be provided."
//             });
//         }
//
//         // Simple URI validation: all URIs must be absolute (not shown here, but essential).
//         if (request.RedirectUris.Any(uri => !Uri.TryCreate(uri, UriKind.Absolute, out _) || !IsSecureUri(uri)))
//         {
//             return BadRequest(new
//             {
//                 error = "invalid_redirect_uri",
//                 error_description = "Redirect URIs must be valid and use HTTPS, except for loopback/native apps."
//             });
//         }
//         // 3. Credential Generation
//         // Generate a unique Client ID.
//         // var clientId = Guid.NewGuid().ToString("N");
//         var clientId = "mcp_inspector";
//
//         // Generate a Client Secret if the auth method requires one.
//         // string? clientSecret = null;
//         // if (request.TokenEndpointAuthMethod == "client_secret_basic" ||
//         //     request.TokenEndpointAuthMethod == "client_secret_post")
//         // {
//         //     // Generate a strong, high-entropy secret (e.g., 256 bits/32 bytes)
//         //     clientSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
//         // }
//         var clientSecret = "claude code";
//
//         if (ClientStore.TryAdd(clientId, request))
//         {
//             // 4. Persistence (Save Client Configuration)
//         }
//
//         // 5. Generate Response (RFC 7591)
//         // Generate a Registration Access Token (RAT) for subsequent DCR-Management (DCRM)
//         // In production, this would be a real, short-lived JWT scoped for client management.
//         var registrationAccessToken = GenerateRegistrationAccessToken();
//
//         var response = new ClientRegistrationResponse
//         {
//             ClientId = clientId,
//             ClientSecret = clientSecret,
//             ClientIdIssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
//             // The client secret is usually non-expiring, but can be set if rotation is enforced.
//             ClientSecretExpiresAt = 0,
//             RegistrationAccessToken = registrationAccessToken,
//             // The URL for the DCRM endpoint (used for GET/PUT/DELETE)
//             RegistrationClientUri = $"{Request.Scheme}://{Request.Host}/register/{clientId}",
//
//             // Echo back all the client metadata (RFC 7591 requirement)
//             RedirectUris = request.RedirectUris,
//             GrantTypes = request.GrantTypes,
//             ResponseTypes = request.ResponseTypes,
//             ApplicationType = request.ApplicationType,
//             ClientName = request.ClientName,
//             TokenEndpointAuthMethod = request.TokenEndpointAuthMethod,
//             LogoUri = request.LogoUri,
//             PolicyUri = request.PolicyUri,
//             TosUri = request.TosUri
//         };
//
//         // 6. Return 201 Created
//         return Created(response.RegistrationClientUri, response);
//     }
//
//     private static bool IsSecureUri(string uri)
//     {
//         if (uri.StartsWith("https://"))
//             return true;
//
//         // Allow localhost and 127.0.0.1 on any port
//         if (uri.StartsWith("http://localhost") || uri.StartsWith("http://127.0.0.1") || uri.StartsWith("http://[::1]"))
//             return true;
//
//         // Allow custom URI schemes for native apps (e.g., myapp://)
//         if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) && parsedUri.Scheme != "http" && parsedUri.Scheme != "https")
//             return true;
//
//         return false;
//     }
//
//     /// <summary>
//     /// Placeholder for generating a real token for the management endpoint.
//     /// In production, this would be a JWT issued by the AS.
//     /// </summary>
//     private static string GenerateRegistrationAccessToken()
//     {
//         // Simulate a strong, unique token.
//         return $"rat-{Guid.NewGuid().ToString("N")}";
//     }
//
//     // TODO: it does fail when enabled...probably because it is missing the register
//     // ...
//     /// <summary>
//     /// OAuth 2.0 Authorization Server Metadata endpoint (RFC 8414) with MCP extensions
//     /// Returns metadata about the OAuth 2.0 authorization server
//     /// </summary>
//     [HttpGet("~/.well-known/oauth-authorization-server")]
//     public async Task<IActionResult> OAuthMetadata()
//     {
//         // Add MCP-Protocol-Version header as recommended by MCP spec
//         Response.Headers["MCP-Protocol-Version"] = "2025-03-26";
//
//         var baseUrl = $"{Request.Scheme}://{Request.Host}";
//
//         // Try to fetch OIDC configuration from external provider
//         var oidcMetadata = await _oidcConfigService.GetOAuthMetadataAsync(Request);
//
//         if (oidcMetadata != null)
//         {
//             _logger.LogInformation("Returning proxied OIDC configuration from external provider");
//
//             // Override registration endpoint to always use local implementation
//             // This allows clients to register with this MCP server even when using external OIDC
//             oidcMetadata.RegistrationEndpoint = $"{baseUrl}/register";
//             oidcMetadata.GrantTypesSupported =
//             [
//                 "authorization_code",
//                 // "client_credentials",
//                 // "refresh_token",
//                 // "implicit",
//                 // "urn:ietf:params:oauth:grant-type:device_code"
//             ];
//             oidcMetadata.ScopesSupported = [
//                 "openid", 
//                 "profile", 
//                 "tools:read",
//                 "tools:execute",
//                 "tools:admin"                
//             ];
//
//             return Ok(oidcMetadata);
//         }
//
//         // Fallback to local OAuth implementation if OIDC is not configured
//         _logger.LogWarning("OIDC provider not configured, falling back to local OAuth implementation");
//
//         var metadata = new OAuthServerMetadata
//         {
//             Issuer = baseUrl,
//             AuthorizationEndpoint = $"{baseUrl}/authorize",
//             TokenEndpoint = $"{baseUrl}/token",
//             GrantTypesSupported = new[] { "authorization_code", "password" },
//             ResponseTypesSupported = new[] { "code" },
//             CodeChallengeMethodsSupported = new[] { "S256", "plain" }, // PKCE support (S256 recommended)
//             TokenEndpointAuthMethodsSupported = new[] { "none", "client_secret_post", "client_secret_basic" },
//             ScopesSupported = new[] { "tools:read", "tools:execute", "tools:admin" },
//             RegistrationEndpoint = $"{baseUrl}/register", // RFC 7591 Dynamic Client Registration
//             RevocationEndpoint = null, // Can be added later if needed
//             IntrospectionEndpoint = null, // Can be added later if needed
//             JwksUri = null // Not using public key cryptography for JWT in this demo
//             
//         };
//
//         return Ok(metadata);
//     }
//
//     /// <summary>
//     /// OAuth 2.0 Protected Resource Metadata endpoint (RFC 9470)
//     /// Returns metadata about the protected resource server
//     /// </summary>
//     [HttpGet("~/.well-known/oauth-protected-resource")]
//     public IActionResult OAuthProtectedResourceMetadata()
//     {
//         var baseUrl = $"{Request.Scheme}://{Request.Host}";
//
//         // Add MCP-Protocol-Version header as recommended by MCP spec
//         Response.Headers["MCP-Protocol-Version"] = "2025-03-26";
//
//         var metadata = new OAuthProtectedResourceMetadata
//         {
//             Resource = baseUrl,
//             AuthorizationServers = new[] { baseUrl },
//             BearerMethodsSupported = new[] { "header" },
//             ResourceSigningAlgValuesSupported = new[] { "HS256" },
//             ScopesSupported = new[] { "tools:read", "tools:execute", "tools:admin" }
//         };
//
//         _logger.LogInformation("OAuth protected resource metadata requested from {BaseUrl}", baseUrl);
//
//         return Ok(metadata);
//     }
//
//     private void LogRequestMetadata()
//     {
//         var log = new StringBuilder();
//         log.AppendLine("--- HTTP REQUEST START ---");
//         log.AppendLine($"Method: {Request.Method}");
//         log.AppendLine($"Path: {Request.Path}{Request.QueryString}");
//         // log.AppendLine("Headers:");
//         //
//         // foreach (var header in Request.Headers)
//         // {
//         //     // You might want to redact sensitive headers like Authorization
//         //     log.AppendLine($"  {header.Key}: {header.Value}");
//         // }
//
//         _logger.LogInformation(log.ToString());
//     }
//
//     private async Task LogRequest()
//     {
//         LogRequestMetadata();
//
//         // The request body stream is forward-only, so we need to enable 
//         // buffering to allow the downstream model binder to read it.
//         // Request.EnableBuffering();
//
//         // Leave the stream open for the downstream model binder
//         using var reader = new StreamReader(Request.Body, Encoding.UTF8, true, 1024, true);
//         var body = await reader.ReadToEndAsync();
//
//         if (!string.IsNullOrWhiteSpace(body))
//         {
//             _logger.LogInformation("Body:\n{RequestBody}", body);
//         }
//
//         // **CRITICAL**: Reset the stream's position to 0 so the model binder 
//         // can read it again later in the pipeline.
//         // Request.Body.Position = 0;
//     }
// }
//
// /// <summary>
// /// Defines the JSON-RPC standard response structure for a successful result.
// /// </summary>
// /// <typeparam name="T">The type of the result payload (e.g., InitializeResult).</typeparam>
// public class JsonRpcResponse<T>
// {
//     [JsonPropertyName("jsonrpc")] public string JsonRpc { get; } = "2.0";
//
//     [JsonPropertyName("result")] public T Result { get; set; }
//
//     [JsonPropertyName("id")] public int Id { get; set; } // Matches the ID from the incoming request
// }
//
// /// <summary>
// /// The expected payload for a successful 'initialize' response.
// /// </summary>
// public class InitializeResult
// {
//     [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; set; }
//
//     [JsonPropertyName("capabilities")] public Dictionary<string, object> Capabilities { get; set; } // Server capabilities
//
//     [JsonPropertyName("serverInfo")] public ServerInfo ServerInfo { get; set; }
// }
//
// /// <summary>
// /// Server information for initialize response
// /// </summary>
// public class ServerInfo
// {
//     [JsonPropertyName("name")] public string Name { get; set; }
//
//     [JsonPropertyName("version")] public string Version { get; set; }
// }