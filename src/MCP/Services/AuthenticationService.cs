using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;

namespace McpServer.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly ILogger<AuthenticationService> _logger;
    private readonly IOidcConfigurationService _oidcConfigService;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthenticationService(
        ILogger<AuthenticationService> logger,
        IOidcConfigurationService oidcConfigService,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _oidcConfigService = oidcConfigService;
        _httpClientFactory = httpClientFactory;
    }

    public bool ValidateToken(string token)
    {
        return ValidateTokenAsync(token).GetAwaiter().GetResult();
    }

    private async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            // Validate token using OIDC provider's JWKS
            var oidcConfig = await _oidcConfigService.GetConfigurationAsync();
            if (oidcConfig?.JwksUri != null)
            {
                _logger.LogInformation("Validating token using OIDC JWKS from: {JwksUri}", oidcConfig.JwksUri);

                // Try fetching JWKS directly
                var jwks = await FetchJwksDirectlyAsync(oidcConfig.JwksUri);
                if (jwks != null && jwks.Any())
                {
                    _logger.LogInformation("Successfully fetched {KeyCount} keys from JWKS endpoint", jwks.Count);

                    var validationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKeys = jwks,
                        ValidateIssuer = true,
                        ValidIssuer = oidcConfig.Issuer,
                        ValidateAudience = false, // Set to true and configure ValidAudience if needed
                        ClockSkew = TimeSpan.FromMinutes(5)
                    };

                    tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                    _logger.LogInformation("Token validated successfully using directly fetched JWKS");
                    return validatedToken != null;
                }

                _logger.LogWarning("Failed to fetch JWKS directly, falling back to local validation");
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return false;
        }
    }

    public string? GetUsernameFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            return jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract username from token");
            return null;
        }
    }

    private async Task<ICollection<SecurityKey>?> FetchJwksDirectlyAsync(string jwksUri)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(jwksUri);
            response.EnsureSuccessStatusCode();

            var jwksJson = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("JWKS Response: {JwksJson}", jwksJson);

            var jwks = new JsonWebKeySet(jwksJson);
            return jwks.GetSigningKeys();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch JWKS from {JwksUri}", jwksUri);
            return null;
        }
    }
}