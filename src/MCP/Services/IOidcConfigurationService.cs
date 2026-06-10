using McpServer.Models;
using Microsoft.AspNetCore.Http;
using PI.Shared.Models;

namespace McpServer.Services;

/// <summary>
/// Service for fetching and caching OIDC configuration from an external provider
/// </summary>
public interface IOidcConfigurationService
{
    /// <summary>
    /// Gets the OIDC configuration from the configured provider
    /// </summary>
    Task<OidcConfiguration?> GetConfigurationAsync();

    /// <summary>
    /// Gets the OAuth 2.0 server metadata adapted from OIDC configuration
    /// </summary>
    /// <param name="request"></param>
    Task<OAuthServerMetadata?> GetOAuthMetadataAsync(HttpRequest request);

    Task<Result<ClientRegistrationResponse>> RegisterClientAsync(HttpRequest httpRequest, ClientRegistrationRequest request);
    
    Task<Result<OAuthProtectedResourceMetadata>> OAuthProtectedResourceMetadata(HttpRequest request);
}

/// <summary>
/// OIDC Discovery Document model (OpenID Connect Discovery 1.0)
/// </summary>
public class OidcConfiguration
{
    public string? Issuer { get; set; }
    public string? AuthorizationEndpoint { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? UserInfoEndpoint { get; set; }
    public string? JwksUri { get; set; }
    public string? RegistrationEndpoint { get; set; }
    public string? RevocationEndpoint { get; set; }
    public string? IntrospectionEndpoint { get; set; }
    public string[]? ScopesSupported { get; set; }
    public string[]? ResponseTypesSupported { get; set; }
    public string[]? GrantTypesSupported { get; set; }
    public string[]? SubjectTypesSupported { get; set; }
    public string[]? IdTokenSigningAlgValuesSupported { get; set; }
    public string[]? TokenEndpointAuthMethodsSupported { get; set; }
    public string[]? CodeChallengeMethodsSupported { get; set; }
}
