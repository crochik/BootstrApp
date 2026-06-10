using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace McpServer.Models;

// MCP Protocol Models
public class McpRequest
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public object? Params { get; set; }

    [JsonPropertyName("id")]
    public int? Id { get; set; }
}

public class McpResponse
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public McpError? Error { get; set; }

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    // Server-internal: signals to the controller that an SSE-mode caller should
    // also receive a notifications/tools/list_changed frame after this response.
    [JsonIgnore]
    public bool TriggersListChanged { get; set; }

    // Server-internal: signals to the controller that the tool was rejected for
    // missing/invalid auth. The controller still writes the response body (which
    // carries the tool-level isError result so the LLM sees it) but additionally
    // issues a JwtBearer challenge so MCP clients receive the WWW-Authenticate
    // header and can refresh their OAuth token.
    [JsonIgnore]
    public bool RequiresAuthChallenge { get; set; }
}

public class McpNotification
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; set; }
}

public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

// Tool Models
public class ToolMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public ToolInputSchema InputSchema { get; set; } = new();

    [JsonPropertyName("outputSchema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolInputSchema? OutputSchema { get; set; }

    [JsonPropertyName("examplePrompts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ExamplePrompts { get; set; }

    // Internal property, not serialized
    [JsonIgnore]
    public bool RequiresAuthentication { get; set; } = true;

    // Internal property, not serialized. When true the tool is excluded from
    // tools/list output but remains callable via tools/call and discoverable
    // via the built-in tool_search meta-tool.
    [JsonIgnore]
    public bool Deferred { get; set; } = false;

    public ToolMetadata(){}
}

[JsonConverter(typeof(ToolInputSchemaConverter))]
public class ToolInputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, PropertySchema> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new();

    /// <summary>
    /// When set, the schema is emitted from this <see cref="JsonNode"/> verbatim
    /// (replacing Type/Properties/Required). Populated by <c>JsonSchemaBuilder</c>
    /// when a global override is registered for the underlying CLR type.
    /// </summary>
    [JsonIgnore]
    public JsonNode? RawSchema { get; set; }
}

[JsonConverter(typeof(PropertySchemaConverter))]
public class PropertySchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; set; }

    // Set when Type == "object". Allows nested object shapes.
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, PropertySchema>? Properties { get; set; }

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Required { get; set; }

    // Set when Type == "array".
    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PropertySchema? Items { get; set; }

    // String enum values when the underlying CLR type is an enum.
    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Enum { get; set; }

    /// <summary>
    /// When set, the schema for this property is emitted from this <see cref="JsonNode"/>
    /// verbatim (replacing all other fields). Populated by <c>JsonSchemaBuilder</c>
    /// when a global override is registered for the property's CLR type.
    /// </summary>
    [JsonIgnore]
    public JsonNode? RawSchema { get; set; }
}

public class ToolCallRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; set; }

    // Optional MCP protocol field for progress reporting
    [JsonPropertyName("_meta")]
    public Dictionary<string, object>? Meta { get; set; }
}

public class ToolCallResult
{
    [JsonPropertyName("isError")]
    public bool IsError { get; set; }

    [JsonPropertyName("content")]
    public List<ToolContent> Content { get; set; } = new();

    // Strongly-typed payload validated against the tool's outputSchema. The protocol
    // handler decides per-client whether to emit this and/or the legacy Content array.
    [JsonPropertyName("structuredContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? StructuredContent { get; set; }

    // Server-internal: tool sets this when its execution mutated the available
    // tool catalog so that SSE-mode callers receive a notifications/tools/list_changed frame.
    [JsonIgnore]
    public bool TriggersListChanged { get; set; }

    // Enums serialize as their string member names to match the input-schema shape
    // advertised on tools/list — same convention as AttributeToolSource.PrettyJson.
    private static readonly JsonSerializerOptions StructuredJson = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
    };

    public static ToolCallResult Error(string message) => new()
    {
        IsError = true,
        Content = [new ToolContent { Type = "text", Text = message }]
    };

    public static ToolCallResult Text(string text) => new()
    {
        Content = [new ToolContent { Type = "text", Text = text }]
    };

    public static ToolCallResult Structured(object value) => new()
    {
        StructuredContent = value,
        Content = [new ToolContent { Type = "text", Text = JsonSerializer.Serialize(value, StructuredJson) }]
    };
}

public class ToolContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

// Resource Models
public class ResourceMetadata
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; set; }

    // Internal property, not serialized.
    [JsonIgnore]
    public bool RequiresAuthentication { get; set; } = true;
}

public class ResourceContent
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; set; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    // Base64-encoded binary payload. Mutually exclusive with Text per MCP spec.
    [JsonPropertyName("blob")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Blob { get; set; }
}

public class ResourceReadResult
{
    [JsonPropertyName("contents")]
    public List<ResourceContent> Contents { get; set; } = new();

    // Server-internal: surfaced through the resources/read response payload as
    // an extra "isError" field so the LLM sees the message in-band, mirroring
    // the ToolCallResult.Error pattern.
    [JsonIgnore]
    public bool IsError { get; set; }

    public static ResourceReadResult Text(string uri, string text, string? mimeType = "text/plain") =>
        new() { Contents = [new ResourceContent { Uri = uri, Text = text, MimeType = mimeType }] };

    public static ResourceReadResult Blob(string uri, byte[] data, string? mimeType = "application/octet-stream") =>
        new() { Contents = [new ResourceContent { Uri = uri, Blob = Convert.ToBase64String(data), MimeType = mimeType }] };

    public static ResourceReadResult Error(string message) => new()
    {
        IsError = true,
        Contents = [new ResourceContent { Text = message, MimeType = "text/plain" }]
    };
}

public class ResourcesReadRequest
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;
}

// Authentication Models
public class LoginRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class TokenRequest
{
    [JsonPropertyName("grant_type")]
    public string GrantType { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("redirect_uri")]
    public string? RedirectUri { get; set; }

    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    [JsonPropertyName("client_secret")]
    public string? ClientSecret { get; set; }

    [JsonPropertyName("code_verifier")]
    public string? CodeVerifier { get; set; }
}

public class AuthorizationRequest
{
    [JsonPropertyName("response_type")]
    public string ResponseType { get; set; } = string.Empty;

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("redirect_uri")]
    public string? RedirectUri { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("code_challenge")]
    public string? CodeChallenge { get; set; }

    [JsonPropertyName("code_challenge_method")]
    public string? CodeChallengeMethod { get; set; }
}

public class AuthorizationCode
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("redirect_uri")]
    public string? RedirectUri { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("code_challenge")]
    public string? CodeChallenge { get; set; }

    [JsonPropertyName("code_challenge_method")]
    public string? CodeChallengeMethod { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("is_used")]
    public bool IsUsed { get; set; }
}

// OAuth 2.0 Server Metadata (RFC 8414) with MCP extensions
public class OAuthServerMetadata
{
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("grant_types_supported")]
    public string[] GrantTypesSupported { get; set; } = Array.Empty<string>();

    [JsonPropertyName("response_types_supported")]
    public string[] ResponseTypesSupported { get; set; } = Array.Empty<string>();

    [JsonPropertyName("code_challenge_methods_supported")]
    public string[] CodeChallengeMethodsSupported { get; set; } = Array.Empty<string>();

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public string[] TokenEndpointAuthMethodsSupported { get; set; } = Array.Empty<string>();

    [JsonPropertyName("scopes_supported")]
    public string[] ScopesSupported { get; set; } = Array.Empty<string>();

    [JsonPropertyName("registration_endpoint")]
    public string? RegistrationEndpoint { get; set; }

    [JsonPropertyName("revocation_endpoint")]
    public string? RevocationEndpoint { get; set; }

    [JsonPropertyName("introspection_endpoint")]
    public string? IntrospectionEndpoint { get; set; }

    [JsonPropertyName("jwks_uri")]
    public string? JwksUri { get; set; }
}

// OAuth 2.0 Protected Resource Metadata (RFC 9470)
public class OAuthProtectedResourceMetadata
{
    [JsonPropertyName("resource")]
    public string Resource { get; set; } = string.Empty;

    [JsonPropertyName("authorization_servers")]
    public string[] AuthorizationServers { get; set; } = Array.Empty<string>();

    [JsonPropertyName("bearer_methods_supported")]
    public string[] BearerMethodsSupported { get; set; } = Array.Empty<string>();

    [JsonPropertyName("resource_signing_alg_values_supported")]
    public string[] ResourceSigningAlgValuesSupported { get; set; } = Array.Empty<string>();

    [JsonPropertyName("scopes_supported")]
    public string[] ScopesSupported { get; set; } = Array.Empty<string>();
}

// --- Request Model (RFC 7591 Client Metadata) ---

/// <summary>
/// Represents the client metadata sent by the client application to the registration endpoint.
/// Uses System.Text.Json for serialization, mapping C# PascalCase to JSON snake_case.
/// </summary>
public record ClientRegistrationRequest
{
    /// <summary>
    /// Array of redirection URI strings for use in the response. (REQUIRED)
    /// </summary>
    [JsonPropertyName("redirect_uris")]
    public required string[] RedirectUris { get; init; }

    /// <summary>
    /// OAuth 2.0 grant type strings that the client will restrict itself to using.
    /// e.g., "authorization_code", "client_credentials", "refresh_token".
    /// </summary>
    [JsonPropertyName("grant_types")]
    public string[]? GrantTypes { get; init; } = ["authorization_code"];

    /// <summary>
    /// A list of the OAuth 2.0 response type strings that the client can utilize.
    /// e.g., "code", "token".
    /// </summary>
    [JsonPropertyName("response_types")]
    public string[]? ResponseTypes { get; init; } = ["code"];

    /// <summary>
    /// The client application type. e.g., "web" or "native".
    /// </summary>
    [JsonPropertyName("application_type")]
    public string? ApplicationType { get; init; } = "web";

    /// <summary>
    /// Human-readable name of the client to be presented to the end-user during authorization.
    /// </summary>
    [JsonPropertyName("client_name")]
    public required string ClientName { get; init; }

    /// <summary>
    /// Client authentication method used at the token endpoint.
    /// e.g., "client_secret_basic", "client_secret_post", "none".
    /// </summary>
    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; init; } = "client_secret_basic";

    /// <summary>
    /// URL for the client's logo image.
    /// </summary>
    [JsonPropertyName("logo_uri")]
    public string? LogoUri { get; init; }

    /// <summary>
    /// URL of the client's privacy policy.
    /// </summary>
    [JsonPropertyName("policy_uri")]
    public string? PolicyUri { get; init; }

    /// <summary>
    /// URL of the client's terms of service.
    /// </summary>
    [JsonPropertyName("tos_uri")]
    public string? TosUri { get; init; }
}

// --- Response Model (RFC 7591 Registration Response) ---

/// <summary>
/// Represents the response returned by the authorization server after successful registration.
/// Must include all the client metadata that was registered.
/// </summary>
public record ClientRegistrationResponse
{
    /// <summary>
    /// Unique identifier for the client. (REQUIRED)
    /// </summary>
    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    /// <summary>
    /// The client secret. Included if 'token_endpoint_auth_method' is secret-based. (OPTIONAL)
    /// </summary>
    [JsonPropertyName("client_secret")]
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Time at which the client was registered. Unix timestamp (seconds since epoch).
    /// </summary>
    [JsonPropertyName("client_id_issued_at")]
    public long ClientIdIssuedAt { get; init; }

    /// <summary>
    /// Time at which the client secret will expire. Unix timestamp (seconds since epoch). (OPTIONAL)
    /// </summary>
    [JsonPropertyName("client_secret_expires_at")]
    public long? ClientSecretExpiresAt { get; init; } = 0; // 0 indicates non-expiring

    /// <summary>
    /// The URL for reading, updating, and deleting the client configuration. (OPTIONAL)
    /// </summary>
    [JsonPropertyName("registration_client_uri")]
    public string? RegistrationClientUri { get; init; }

    /// <summary>
    /// An access token used to authenticate calls to the 'registration_client_uri'. (OPTIONAL)
    /// </summary>
    [JsonPropertyName("registration_access_token")]
    public string? RegistrationAccessToken { get; init; }

    // Echo back all the client metadata from the request (RFC 7591 requirement)

    [JsonPropertyName("redirect_uris")]
    public string[] RedirectUris { get; init; } = Array.Empty<string>();

    [JsonPropertyName("grant_types")]
    public string[]? GrantTypes { get; init; }

    [JsonPropertyName("response_types")]
    public string[]? ResponseTypes { get; init; }

    [JsonPropertyName("application_type")]
    public string? ApplicationType { get; init; }

    [JsonPropertyName("client_name")]
    public string? ClientName { get; init; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; init; }

    [JsonPropertyName("logo_uri")]
    public string? LogoUri { get; init; }

    [JsonPropertyName("policy_uri")]
    public string? PolicyUri { get; init; }

    [JsonPropertyName("tos_uri")]
    public string? TosUri { get; init; }
}