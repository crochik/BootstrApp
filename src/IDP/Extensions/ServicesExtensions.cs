using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Newtonsoft.Json;

namespace IDP;

public static class ServicesExtensions
{
    public static IServiceCollection AddAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var section = config.GetSection("Authentication");

        // clear current mapping so all claims are unmodified
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        // https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/?view=aspnetcore-3.0&tabs=visual-studio
        // https://identityserver4.readthedocs.io/en/latest/quickstarts/2_interactive_aspnetcore.html#refexternalauthenticationquickstart
        var builder = services
            // .AddOidcStateDataFormatterCache() // https://docs.identityserver.io/en/latest/topics/signin_external_providers.html?highlight=cookie#the-role-of-cookies
            .AddAuthentication(Consts.DefaultAuthenticationScheme)

            // Attempt to avoid cookies (https://www.daimto.com/headersize-issue-with-identityserver4/)
            .AddCookie(Consts.ExternalCookieAuthenticationScheme, options => options.SessionStore = MongoCacheTicketStore.Get())
            .AddCookie(Consts.DefaultAuthenticationScheme, options => options.SessionStore = MongoCacheTicketStore.Get())

            // Add authentication using "itself"
            .AddJwtBearer(options =>
            {
                var authConfig = PI.Shared.App.MicroserviceApp.AuthenticationConfig.Get(config);
                options.Authority = authConfig.Authority;
                options.RequireHttpsMetadata = options.Authority.StartsWith("https");
                options.Audience = authConfig.APIName;

                options.TokenValidationParameters.RoleClaimType = JwtClaimTypes.Role;
                options.TokenValidationParameters.NameClaimType = JwtClaimTypes.Name;
            })
            
            .AddMicrosoft(section.GetSection("Microsoft").Get<OAuthAccountConfig>())
            .AddGoogle(section.GetSection("Google").Get<OAuthAccountConfig>())
            .AddOkta(section.GetSection("Okta").Get<OpenIdConfig>())
            .AddSalesforceUsingOIDC(section.GetSection("Salesforce").Get<OpenIdConfig>())
            .AddTypeform(section.GetSection("Typeform").Get<OAuthAccountConfig>())
            .AddGitHub(section.GetSection("GitHub").Get<OAuthAccountConfig>())
            ;

        builder.AddJwtBearer(Consts.AnonymousAuthenticationScheme, "Anonymous", configureOptions =>
        {
        });

        // Per-AppClient dynamic auth providers. The bare scheme registrations above remain the
        // global path (used when nothing in the AppClient document overrides them). Composite
        // scheme names ("Provider:ClientId") are resolved on demand against AppClient.AuthenticationProviders.
        services.AddSingleton<ProviderResolver>();
        services.RemoveAll<IAuthenticationSchemeProvider>();
        services.AddSingleton<IAuthenticationSchemeProvider, DynamicAuthenticationSchemeProvider>();
        services.AddSingleton<IOptionsFactory<GoogleOptions>,           PerClientOptionsFactory<GoogleOptions>>();
        services.AddSingleton<IOptionsFactory<MicrosoftAccountOptions>, PerClientOptionsFactory<MicrosoftAccountOptions>>();
        services.AddSingleton<IOptionsFactory<OAuthOptions>,            PerClientOptionsFactory<OAuthOptions>>();
        services.AddSingleton<IOptionsFactory<OpenIdConnectOptions>,    PerClientOptionsFactory<OpenIdConnectOptions>>();

        return services;
    }

    // connect using felipecrochik@hotmail.com to         
    //      https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/Overview/appId/8b6de780-0e2b-4564-a071-85b472840b5d/isMSAApp/
    //      https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/Overview/appId/8b6de780-0e2b-4564-a071-85b472840b5d/isMSAApp~/false
    // old urls:
    //      https://apps.dev.microsoft.com/ 
    //      https://apps.dev.microsoft.com/#/application/2f26389e-c236-48db-9436-c8e68a675d33
    // admin access: https://docs.microsoft.com/en-us/graph/auth-v2-service
    private static AuthenticationBuilder AddMicrosoft(this AuthenticationBuilder builder, OAuthAccountConfig config)
    {
        if (config == null) return builder;
        
        return builder.AddMicrosoftAccount(options =>
        {
            options.SignInScheme = Consts.ExternalCookieAuthenticationScheme;

            options.ClientId = config.ClientId;
            options.ClientSecret = config.ClientSecret;
            options.SaveTokens = config.SaveTokens;

            if (config.Scopes != null)
            {
                foreach (var scope in config.Scopes)
                {
                    options.Scope.Add(scope);
                }
            }
        });
    }
    
    // https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-2.2
    // https://developers.google.com/identity/sign-in/web/sign-in?authuser=2
    private static AuthenticationBuilder AddGoogle(this AuthenticationBuilder builder, OAuthAccountConfig config)
    {
        if (config == null) return builder;

        builder.AddGoogle(options =>
        {
            options.SignInScheme = Consts.ExternalCookieAuthenticationScheme;

            options.ClientId = config.ClientId;
            options.ClientSecret = config.ClientSecret;
            options.AccessType = "offline";
            options.SaveTokens = config.SaveTokens;

            if (config.Scopes != null)
            {
                foreach (var scope in config.Scopes)
                {
                    options.Scope.Add(scope);
                }
            }
        });

        // migration to 3.0 recommendation
        // builder.AddOpenIdConnect("Google", o =>
        // {
        //     o.ClientId = Configuration["Authentication:Google:ClientId"];
        //     o.ClientSecret = Configuration["Authentication:Google:ClientSecret"];
        //     o.Authority = "https://accounts.google.com";
        //     o.ResponseType = OpenIdConnectResponseType.Code;
        //     o.CallbackPath = "/signin-google"; // Or register the default "/sigin-oidc"
        //     o.Scope.Add("email");
        // });
        // JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        return builder;
    }
    
        private static AuthenticationBuilder AddTypeform(this AuthenticationBuilder builder, OAuthAccountConfig config)
    {
        if (config == null) return builder;

        return builder.AddOAuth("Typeform",
            options =>
            {
                options.SignInScheme = Consts.ExternalCookieAuthenticationScheme;
                
                options.SaveTokens = config.SaveTokens;
                options.ClientId = config.ClientId;
                options.ClientSecret = config.ClientSecret;
                options.CallbackPath = config.CallbackPath;
                options.AuthorizationEndpoint = "https://api.typeform.com/oauth/authorize";
                options.TokenEndpoint = "https://api.typeform.com/oauth/token";
                options.UserInformationEndpoint = "https://api.typeform.com/me";
                options.Events.OnCreatingTicket = async context =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                    var response = await context.Backchannel.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException($"An error occurred when retrieving Google user information ({response.StatusCode}). Please check if the authentication information is correct.");
                    }

                    var body = await response.Content.ReadAsStringAsync();
                    
                    // using (var payload = JsonDocument.Parse(body))
                    // {
                    // context.User
                    // }
                    // context.RunClaimActions();
                    
                    var user = JsonConvert.DeserializeObject<TypeformUser>(body);
                    context.Identity.AddClaim(new Claim(JwtClaimTypes.Subject, user.UserId));
                    context.Identity.AddClaim(new Claim(JwtClaimTypes.Name, user.Alias));
                    context.Identity.AddClaim(new Claim(JwtClaimTypes.Email, user.Email));
                };
                
                // https://api.typeform.com/me
                
                foreach (var scope in config.Scopes)
                {
                    options.Scope.Add(scope);
                }
            }
        );
    }    

    private static AuthenticationBuilder AddGitHub(this AuthenticationBuilder builder, OAuthAccountConfig config)
    {
        if (config == null) return builder;
        
        return builder.AddOAuth("GitHub",
            options =>
            {
                options.SignInScheme = Consts.ExternalCookieAuthenticationScheme;
                
                options.SaveTokens = config.SaveTokens;
                options.ClientId = config.ClientId;
                options.ClientSecret = config.ClientSecret;
                options.CallbackPath = new PathString("/signin-github");

                options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                options.TokenEndpoint = "https://github.com/login/oauth/access_token";
                options.UserInformationEndpoint = "https://api.github.com/user";

                options.ClaimActions.MapJsonKey(JwtClaimTypes.Subject, "login");
                options.ClaimActions.MapJsonKey(JwtClaimTypes.Name, "name");
                options.ClaimActions.MapJsonKey("urn:github:id", "id");
                options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");
                options.ClaimActions.MapJsonKey("urn:github:url", "html_url");

                options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
                {
                    OnCreatingTicket = async context =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
                        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                        var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                        response.EnsureSuccessStatusCode();

                        var user = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                        context.RunClaimActions(user.RootElement);
                    }
                };
            }
        );
    }    
    
    private static AuthenticationBuilder AddOkta(this AuthenticationBuilder builder, OpenIdConfig config)
    {
        if (config == null) return builder;

        return builder.AddOpenIdConnect("Okta",
            options =>
            {
                // options.SignInScheme = Consts.ExternalCookieAuthenticationScheme;
                // options.SignOutScheme

                options.RequireHttpsMetadata = true;
                options.ResponseType = OpenIdConnectResponseType.CodeIdToken;
                options.SaveTokens = config.SaveTokens;
                options.GetClaimsFromUserInfoEndpoint = true;

                options.ClientId = config.ClientId;
                options.ClientSecret = config.ClientSecret;
                options.Authority = config.Authority;
                options.CallbackPath = config.CallbackPath;
                foreach (var scope in config.Scopes)
                {
                    options.Scope.Add(scope);
                }
            }
        );
    }

    private static AuthenticationBuilder AddSalesforceUsingOIDC(this AuthenticationBuilder builder, OpenIdConfig config)
    {
        if (config == null) return builder;

        // builder.AddSalesforce(options =>
        // {
        //     options.ClientId = "...";
        //     options.ClientSecret = "...";
        //     options.SaveTokens = true;
        //     options.Scope.Add("offline_access");
        // });

        // https://na73.salesforce.com/.well-known/openid-configuration
        builder.AddOpenIdConnect("Salesforce",
            options =>
            {
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.NonceCookie.SameSite = SameSiteMode.None;
                options.NonceCookie.HttpOnly = true;
                
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.CorrelationCookie.SameSite = SameSiteMode.None;
                options.CorrelationCookie.HttpOnly = true;

                // options.ProtocolValidator.RequireNonce = false;
                
                options.RequireHttpsMetadata = true;
                options.ResponseType = OpenIdConnectResponseType.Code; //IdTokenToken;
                options.SaveTokens = true;
                options.ClientId = config.ClientId;
                options.ClientSecret = config.ClientSecret;
                options.Authority = config.Authority;
                options.CallbackPath = config.CallbackPath;
                foreach (var scope in config.Scopes)
                {
                    options.Scope.Add(scope);
                }
                
                // options.GetClaimsFromUserInfoEndpoint = true;

                options.Events = new OpenIdConnectEvents
                {
                    OnRemoteFailure = context =>
                    {
                        context.Response.Redirect("/Home/Error?message=" + context.Failure.Message);
                        context.HandleResponse();
                        return Task.FromResult(0);
                    }
                };                
            }
        );
        

        // builder.AddOAuth("Salesforce", options =>
        // {
        //     // Your Salesforce Connected App credentials
        //     options.ClientId = config.ClientId;
        //     options.ClientSecret = config.ClientSecret;
        //     options.CallbackPath = new PathString("/signin-salesforce");
        //
        //     // Salesforce OAuth 2.0 endpoints
        //     options.AuthorizationEndpoint = "https://login.salesforce.com/services/oauth2/authorize";
        //     options.TokenEndpoint = "https://login.salesforce.com/services/oauth2/token";
        //     options.UserInformationEndpoint = "https://login.salesforce.com/services/oauth2/userinfo";
        //
        //     // Request the necessary scopes
        //     options.Scope.Add("id");
        //     options.Scope.Add("api");
        //
        //     // Map user claims from the Salesforce response
        //     options.ClaimActions.MapJsonKey("urn:salesforce:id", "user_id");
        //     options.ClaimActions.MapJsonKey("urn:salesforce:instance_url", "instance_url");
        //
        //     // This event handler is crucial for fetching user information and building a ClaimsPrincipal
        //     options.Events = new OAuthEvents
        //     {
        //         OnCreatingTickets = async context =>
        //         {
        //             var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
        //             request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        //             request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        //
        //             var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        //             response.EnsureSuccessStatusCode();
        //
        //             var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        //             context.RunClaimActions(user.RootElement);
        //         }
        //     };
        // });        

        return builder;
    }

    public class OAuthAccountConfig
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public bool SaveTokens { get; set; }
        public string[] Scopes { get; set; }
        
        public string CallbackPath { get; set; }
    }

    public class OpenIdConfig : OAuthAccountConfig
    {
        public string Authority { get; set; }
    }

    /// <summary>
    /// </summary>
    /*
    {
           "alias": "Felipe Crochik",
           "email": "ingeniousmind@gmail.com",
           "language": "en",
           "user_id": "01HY1ERA904XR4PYVQAWC7BFYK",
           "tracking_id": 28616506`
    }
    */
    public class TypeformUser
    {
        public string Alias { get; set; }    
        public string Email { get; set; }    
        public string Language { get; set; }
        
        [JsonProperty("user_id")]
        public string UserId { get; set; }
        
        [JsonProperty("tracking_id")]
        public int TrackingId { get; set; }    
    }
}