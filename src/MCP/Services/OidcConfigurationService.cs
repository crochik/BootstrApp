using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Crochik.Mongo;
using McpServer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;
using PI.Shared.Models.Client;

namespace McpServer.Services;

/// <summary>
/// Service that fetches OIDC configuration from an external provider and caches it
/// </summary>
public class OidcConfigurationService(
    IConfiguration configuration,
    MongoConnection connection,
    ILogger<OidcConfigurationService> logger,
    IHttpClientFactory httpClientFactory)
    : IOidcConfigurationService
{
    private static string[] Scopes =
    [
        "openid",
        "profile",
        // "email",
        "offline_access",
        "mcp:tools",
        "mcp:resources",
        "mcp:prompts",
    ];

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private OidcConfiguration? _cachedConfig;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(60); // Cache for 1 hour

    public async Task<OidcConfiguration?> GetConfigurationAsync()
    {
        // Return cached configuration if still valid
        if (_cachedConfig != null && DateTime.UtcNow < _cacheExpiry)
        {
            logger.LogDebug("Returning cached OIDC configuration");
            return _cachedConfig;
        }

        var oidcDiscoveryUrl = configuration["Oidc:DiscoveryUrl"];

        if (string.IsNullOrEmpty(oidcDiscoveryUrl))
        {
            logger.LogWarning("OIDC Discovery URL not configured");
            return null;
        }

        try
        {
            logger.LogInformation("Fetching OIDC configuration from: {Url}", oidcDiscoveryUrl);

            var response = await _httpClient.GetAsync(oidcDiscoveryUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            // Deserialize OIDC discovery document (uses explicit JsonPropertyName attributes)
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            _cachedConfig = JsonSerializer.Deserialize<OidcConfigurationDto>(json, options)?.ToOidcConfiguration();
            _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);

            logger.LogInformation("Successfully fetched and cached OIDC configuration");
            return _cachedConfig;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch OIDC configuration from {Url}", oidcDiscoveryUrl);
            return null;
        }
    }

    public async Task<OAuthServerMetadata?> GetOAuthMetadataAsync(HttpRequest request)
    {
        var oidcConfig = await GetConfigurationAsync();

        var baseUrl = $"{request.Scheme}://{request.Host}";

        if (oidcConfig == null)
        {
            // return new OAuthServerMetadata
            // {
            //     Issuer = baseUrl,
            //     AuthorizationEndpoint = $"{baseUrl}/authorize",
            //     TokenEndpoint = $"{baseUrl}/token",
            //     CodeChallengeMethodsSupported = ["S256", "plain"], // PKCE support (S256 recommended)
            //     TokenEndpointAuthMethodsSupported = ["none", "client_secret_post", "client_secret_basic"],
            //     RegistrationEndpoint = $"{baseUrl}/register", // RFC 7591 Dynamic Client Registration
            //     RevocationEndpoint = null, // Can be added later if needed
            //     IntrospectionEndpoint = null, // Can be added later if needed
            //     JwksUri = null, // Not using public key cryptography for JWT in this demo
            //     // GrantTypesSupported = new[] { "authorization_code", "password" },
            //     // ScopesSupported = new[] { "tools:read", "tools:execute", "tools:admin" },
            //     GrantTypesSupported =
            //     [
            //         "authorization_code",
            //         // "client_credentials",
            //         // "refresh_token",
            //         // "implicit",
            //         // "urn:ietf:params:oauth:grant-type:device_code"
            //     ],
            //     ResponseTypesSupported =
            //     [
            //         "code"
            //     ],
            //     ScopesSupported = [
            //         "openid",
            //         "profile",
            //         "tools:read",
            //         "tools:execute",
            //         "tools:admin"
            //     ],
            // };

            return null;
        }

        // Map OIDC configuration to OAuth 2.0 Server Metadata
        return new OAuthServerMetadata
        {
            Issuer = oidcConfig.Issuer ?? "",
            AuthorizationEndpoint = oidcConfig.AuthorizationEndpoint ?? "",
            TokenEndpoint = oidcConfig.TokenEndpoint ?? "",
            RevocationEndpoint = oidcConfig.RevocationEndpoint,
            IntrospectionEndpoint = oidcConfig.IntrospectionEndpoint,
            CodeChallengeMethodsSupported = oidcConfig.CodeChallengeMethodsSupported ?? ["S256"],
            TokenEndpointAuthMethodsSupported = oidcConfig.TokenEndpointAuthMethodsSupported ?? ["client_secret_basic"],
            JwksUri = oidcConfig.JwksUri,
            // RegistrationEndpoint = oidcConfig.RegistrationEndpoint,
            // GrantTypesSupported = oidcConfig.GrantTypesSupported ?? ["authorization_code"],
            // ResponseTypesSupported = oidcConfig.ResponseTypesSupported ?? ["code"],
            // ScopesSupported = oidcConfig.ScopesSupported ?? ["openid", "profile", "email"],
            RegistrationEndpoint = $"{baseUrl}/register", // RFC 7591 Dynamic Client Registration
            GrantTypesSupported =
            [
                "authorization_code",
                "refresh_token",
                // "client_credentials",
                // "implicit",
                // "urn:ietf:params:oauth:grant-type:device_code"
            ],
            ResponseTypesSupported =
            [
                "code"
            ],
            ScopesSupported = Scopes,
        };
    }

    /*
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

       if (oidcMetadata != null)
       {
           _logger.LogInformation("Returning proxied OIDC configuration from external provider");

           // Override registration endpoint to always use local implementation
           // This allows clients to register with this MCP server even when using external OIDC
           oidcMetadata.ScopesSupported = ;

           return Ok(oidcMetadata);
       }

       // Fallback to local OAuth implementation if OIDC is not configured
       _logger.LogWarning("OIDC provider not configured, falling back to local OAuth implementation");

       var metadata = ;

     */

    public async Task<Result<ClientRegistrationResponse>> RegisterClientAsync(HttpRequest httpRequest, ClientRegistrationRequest request)
    {
        // await LogRequest();
        // _logger.LogInformation("Body: {body}", JsonSerializer.Serialize(request));

        // 1. Initial Access Token (IAT) Check (Security)
        // DCR endpoints are typically protected by an Initial Access Token (IAT).
        // If open registration is allowed, this step can be skipped.
        // TODO: Enable IAT validation in production environments
        // var authHeader = Request.Headers.Authorization.FirstOrDefault();
        // if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        // {
        //     // In a production app, you would validate the IAT against an active token store.
        //     // For this example, we allow open registration for development/testing.
        //     return Unauthorized();
        // }

        // 2. Input Validation (RFC 7591)
        // Check for required fields and proper format.
        if (request.RedirectUris.Length == 0 || string.IsNullOrEmpty(request.ClientName))
        {
            return Result.Error<ClientRegistrationResponse>("invalid_client_metadata");
        }

        // Simple URI validation: all URIs must be absolute (not shown here, but essential).
        if (request.RedirectUris.Any(uri => !Uri.TryCreate(uri, UriKind.Absolute, out _) || !IsSecureUri(uri)))
        {
            return Result.Error<ClientRegistrationResponse>("invalid_redirect_uri");
        }

        if (!request.GrantTypes?.All(x => x is "authorization_code" or "refresh_token") ?? false)
        {
            return Result.Error<ClientRegistrationResponse>("grant type must be authorization_code and refresh_token");
        }

        var clientId = $"mcp_{Guid.NewGuid():N}";
        var clientSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret));

        // create client 
        var client = await connection.InsertAsync(new AppClient
        {
            ClientId = clientId,
            ProfileKey = "MCP",
            ClientName = request.ClientName,
            Description = $"MCP Auto registered Client: {request.ClientName}",
            RequireClientSecret = true,
            ClientSecrets =
            [
                new Secret
                {
                    Created = DateTime.UtcNow,
                    // Expiration = DateTime.UtcNow.AddDays(1),
                    Description = "Auto generated client secret",
                    // Type = "S256",
                    Value = Convert.ToBase64String(hash),
                }
            ],
            AllowedGrantTypes =
            [
                new ClientGrantType { GrantType = "authorization_code" },
                new ClientGrantType { GrantType = "refresh_token" },
            ],
            AuthenticationProviders = new Dictionary<string, AuthenticationProvider>
            {
                { "Salesforce", new AuthenticationProvider() },
                { "Microsoft", new AuthenticationProvider() },
                { "Google", new AuthenticationProvider() },
                { "GitHub", new AuthenticationProvider() },
            },
            AllowedScopes = Scopes.Select(x => new ClientScope { Scope = x }).ToList(),
            RedirectUris = request.RedirectUris.Select(x => new ClientRedirectUri { RedirectUri = x }).ToList(),
            AllowedCorsOrigins = [], //???
            RequirePkce = true,
            AllowOfflineAccess = true,
            RequireConsent = true,
            AllowRememberConsent = true,
            // RequireRequestObject = false,
            IncludeJwtId = true,
            Created = DateTime.UtcNow,
            UpdateAccessTokenClaimsOnRefresh = true,
            Claims = [],
        });

        // 5. Generate Response (RFC 7591)
        // Generate a Registration Access Token (RAT) for subsequent DCR-Management (DCRM)
        // In production, this would be a real, short-lived JWT scoped for client management.
        var registrationAccessToken = GenerateRegistrationAccessToken();

        var response = new ClientRegistrationResponse
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientIdIssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            // The client secret is usually non-expiring, but can be set if rotation is enforced.
            ClientSecretExpiresAt = 0,
            RegistrationAccessToken = registrationAccessToken,
            // The URL for the DCRM endpoint (used for GET/PUT/DELETE)
            RegistrationClientUri = $"{httpRequest.Scheme}://{httpRequest.Host}/register/{clientId}",

            // Echo back all the client metadata (RFC 7591 requirement)
            RedirectUris = request.RedirectUris,
            GrantTypes = request.GrantTypes,
            ResponseTypes = request.ResponseTypes,
            ApplicationType = request.ApplicationType,
            ClientName = request.ClientName,
            TokenEndpointAuthMethod = request.TokenEndpointAuthMethod,
            LogoUri = request.LogoUri,
            PolicyUri = request.PolicyUri,
            TosUri = request.TosUri
        };

        return Result.Success(response);
    }

    public async Task<Result<OAuthProtectedResourceMetadata>> OAuthProtectedResourceMetadata(HttpRequest request)
    {
        var resource = $"{request.Scheme}://{request.Host}/mcp";
        var baseUrl = $"{request.Scheme}://{request.Host}";

        var oidc = await GetConfigurationAsync();
        if (string.IsNullOrEmpty(oidc?.Issuer))
        {
            return Result.Error<OAuthProtectedResourceMetadata>("OIDC issuer unavailable; cannot serve protected-resource metadata");
        }
        
        var metadata = new OAuthProtectedResourceMetadata
        {
            Resource = resource, // baseUrl,
            AuthorizationServers = [baseUrl],
            BearerMethodsSupported = ["header"],
            // ResourceSigningAlgValuesSupported = ["HS256"],
            // ScopesSupported = new[] { "tools:read", "tools:execute", "tools:admin" }
            ScopesSupported = Scopes,
        };

        return Result.Success(metadata);
    }

    private static bool IsSecureUri(string uri)
    {
        if (uri.StartsWith("https://"))
            return true;

        // Allow localhost and 127.0.0.1 on any port
        if (uri.StartsWith("http://localhost") || uri.StartsWith("http://127.0.0.1") || uri.StartsWith("http://[::1]"))
            return true;

        // Allow custom URI schemes for native apps (e.g., myapp://)
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) && parsedUri.Scheme != "http" && parsedUri.Scheme != "https")
            return true;

        return false;
    }

    /// <summary>
    /// Placeholder for generating a real token for the management endpoint.
    /// In production, this would be a JWT issued by the AS.
    /// </summary>
    private static string GenerateRegistrationAccessToken()
    {
        // Simulate a strong, unique token.
        return $"rat-{Guid.NewGuid().ToString("N")}";
    }

    /// <summary>
    /// DTO for deserializing OIDC discovery document with snake_case properties
    /// </summary>
    private class OidcConfigurationDto
    {
        [JsonPropertyName("issuer")] public string? Issuer { get; set; }

        [JsonPropertyName("authorization_endpoint")]
        public string? AuthorizationEndpoint { get; set; }

        [JsonPropertyName("token_endpoint")] public string? TokenEndpoint { get; set; }

        [JsonPropertyName("userinfo_endpoint")]
        public string? UserinfoEndpoint { get; set; }

        [JsonPropertyName("jwks_uri")] public string? JwksUri { get; set; }

        [JsonPropertyName("registration_endpoint")]
        public string? RegistrationEndpoint { get; set; }

        [JsonPropertyName("revocation_endpoint")]
        public string? RevocationEndpoint { get; set; }

        [JsonPropertyName("introspection_endpoint")]
        public string? IntrospectionEndpoint { get; set; }

        [JsonPropertyName("scopes_supported")] public string[]? ScopesSupported { get; set; }

        [JsonPropertyName("response_types_supported")]
        public string[]? ResponseTypesSupported { get; set; }

        [JsonPropertyName("grant_types_supported")]
        public string[]? GrantTypesSupported { get; set; }

        [JsonPropertyName("subject_types_supported")]
        public string[]? SubjectTypesSupported { get; set; }

        [JsonPropertyName("id_token_signing_alg_values_supported")]
        public string[]? IdTokenSigningAlgValuesSupported { get; set; }

        [JsonPropertyName("token_endpoint_auth_methods_supported")]
        public string[]? TokenEndpointAuthMethodsSupported { get; set; }

        [JsonPropertyName("code_challenge_methods_supported")]
        public string[]? CodeChallengeMethodsSupported { get; set; }

        public OidcConfiguration ToOidcConfiguration() => new()
        {
            Issuer = Issuer,
            AuthorizationEndpoint = AuthorizationEndpoint,
            TokenEndpoint = TokenEndpoint,
            UserInfoEndpoint = UserinfoEndpoint,
            JwksUri = JwksUri,
            RegistrationEndpoint = RegistrationEndpoint,
            RevocationEndpoint = RevocationEndpoint,
            IntrospectionEndpoint = IntrospectionEndpoint,
            ScopesSupported = ScopesSupported,
            ResponseTypesSupported = ResponseTypesSupported,
            GrantTypesSupported = GrantTypesSupported,
            SubjectTypesSupported = SubjectTypesSupported,
            IdTokenSigningAlgValuesSupported = IdTokenSigningAlgValuesSupported,
            TokenEndpointAuthMethodsSupported = TokenEndpointAuthMethodsSupported,
            CodeChallengeMethodsSupported = CodeChallengeMethodsSupported
        };
    }
}