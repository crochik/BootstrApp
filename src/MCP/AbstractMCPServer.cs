using System.Text;
using McpServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PI.Shared.App;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;

namespace MCP;

public abstract class AbstractMCPServer : MicroserviceApp
{
    protected override void AddServices(IServiceCollection services)
    {
        // base.AddServices(services);
        
        services.AddMongoConnection();
        services.AddMongoAdapters();

        // var health = services.AddHealthChecks();
        // AddHealthCheckServices(health);

        // doesn't work, conflicts with openapi
        // services.AddSwaggerGen(AddSwaggerGen);
        // services.AddSwaggerGenNewtonsoftSupport();
            
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                // Round-trip enums as their member names, matching the tool input schemas.
                options.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: true));
            });
        
        services
            .AddObjectTypeService()
            ;

        services.AddSingleton<IDataProtectionServiceProvider, MicrosoftDataProtectionServiceProvider>();

        services.AddSingleton<IOidcConfigurationService, OidcConfigurationService>();
        services.AddSingleton<IMcpProtocolHandler, McpProtocolHandler>();

        // add authentication 
        AddAuthentication(services);
    }

    protected override void UseAuth(IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    protected override void UseSwagger(IApplicationBuilder app)
    {
        // do nothing
    }

    private void AddAuthentication(IServiceCollection services)
    {
        // TODO: some of this code also exist in the OidcConfigurationService
        // consolidate
        // ...
        var oidcDiscoveryUrl = Configuration["Oidc:DiscoveryUrl"];
        var httpClient = new HttpClient();
        var discoveryResponse = httpClient.GetAsync(oidcDiscoveryUrl).GetAwaiter().GetResult();
        discoveryResponse.EnsureSuccessStatusCode();
        var discoveryJson = discoveryResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var doc = System.Text.Json.JsonDocument.Parse(discoveryJson);
        var cachedJwksUri = doc.RootElement.GetProperty("jwks_uri").GetString();
        var cachedIssuer = doc.RootElement.GetProperty("issuer").GetString();
        
        // Fetch and parse JWKS
        var jwksClient = new HttpClient();
        var jwksResponse = jwksClient.GetAsync(cachedJwksUri).GetAwaiter().GetResult();
        jwksResponse.EnsureSuccessStatusCode();
        var jwksJson = jwksResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var jwks = new Microsoft.IdentityModel.Tokens.JsonWebKeySet(jwksJson);        
        
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                // prevent mapping claims
                options.MapInboundClaims = false;
                
                options.RequireHttpsMetadata = false; // Set to true in production
                options.SaveToken = true;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = false, // Set to true and configure ValidAudiences in production
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                // Handle authentication events
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<AbstractMCPServer>>();
                        logger.LogWarning("JWT authentication failed: {Message}", context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<AbstractMCPServer>>();
                        var username = context.Principal?.FindFirst("sub")?.Value ?? "unknown";
                        logger.LogInformation("Token validated for user: {Username}", username);
                        return Task.CompletedTask;
                    },
                    // RFC 9728: point clients at the protected-resource metadata document so
                    // they can discover which authorization server to use. We take over the
                    // response (HandleResponse) so JwtBearerHandler doesn't overwrite our
                    // WWW-Authenticate header with its default.
                    OnChallenge = context =>
                    {
                        context.HandleResponse();

                        var req = context.Request;
                        var resourceMetadataUrl =
                            $"{req.Scheme}://{req.Host}/.well-known/oauth-protected-resource/mcp";

                        var error = context.Error ?? "invalid_token";
                        var sb = new StringBuilder($"Bearer realm=\"mcp\", error=\"{error}\"");
                        if (!string.IsNullOrEmpty(context.ErrorDescription))
                        {
                            sb.Append($", error_description=\"{context.ErrorDescription}\"");
                        }
                        sb.Append($", resource_metadata=\"{resourceMetadataUrl}\"");

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers["WWW-Authenticate"] = sb.ToString();
                        return Task.CompletedTask;
                    }
                };

                // Set up IssuerSigningKeyResolver to dynamically fetch keys from JWKS
                options.TokenValidationParameters.IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                {
                    // Set the issuer in validation parameters
                    validationParameters.ValidIssuer = cachedIssuer;
                    return jwks.GetSigningKeys();
                };
            });
    }
}