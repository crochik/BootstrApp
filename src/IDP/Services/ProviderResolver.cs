using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Crochik.Mongo;
using IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PI.Shared.Models;

namespace IDP;

public static class SchemeName
{
    public const char Separator = ':';

    public static string Compose(string provider, string clientId) => $"{provider}{Separator}{clientId}";

    public static (string Provider, string ClientId) Split(string scheme)
    {
        if (string.IsNullOrEmpty(scheme)) return (scheme, null);
        var i = scheme.IndexOf(Separator);
        return i < 0 ? (scheme, null) : (scheme[..i], scheme[(i + 1)..]);
    }

    public static bool IsComposite(string scheme) =>
        !string.IsNullOrEmpty(scheme) && scheme.IndexOf(Separator) >= 0;
}

public record ProviderDescriptor(
    Type HandlerType,
    Type OptionsType,
    Action<AuthenticationProvider, AuthenticationSchemeOptions> Configure);

public class ProviderResolver
{
    private readonly MongoConnection _mongo;
    private readonly ConcurrentDictionary<string, AppClient> _clientCache = new();

    public ProviderResolver(MongoConnection mongo)
    {
        _mongo = mongo;
    }

    private static readonly Dictionary<string, ProviderDescriptor> _builtins =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Google"]     = new(typeof(GoogleHandler),                  typeof(GoogleOptions),           (ap, o) => ConfigureGoogle(ap, (GoogleOptions)o)),
            ["Microsoft"]  = new(typeof(MicrosoftAccountHandler),        typeof(MicrosoftAccountOptions), (ap, o) => ConfigureMicrosoft(ap, (MicrosoftAccountOptions)o)),
            ["GitHub"]     = new(typeof(OAuthHandler<OAuthOptions>),     typeof(OAuthOptions),            (ap, o) => ConfigureGitHub(ap, (OAuthOptions)o)),
            ["Typeform"]   = new(typeof(OAuthHandler<OAuthOptions>),     typeof(OAuthOptions),            (ap, o) => ConfigureTypeform(ap, (OAuthOptions)o)),
            ["Okta"]       = new(typeof(OpenIdConnectHandler),           typeof(OpenIdConnectOptions),    (ap, o) => ConfigureOkta(ap, (OpenIdConnectOptions)o)),
            ["Salesforce"] = new(typeof(OpenIdConnectHandler),           typeof(OpenIdConnectOptions),    (ap, o) => ConfigureSalesforce(ap, (OpenIdConnectOptions)o)),
        };

    private static readonly Dictionary<string, ProviderDescriptor> _genericTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["oidc"]   = new(typeof(OpenIdConnectHandler),       typeof(OpenIdConnectOptions), (ap, o) => ConfigureGenericOidc(ap, (OpenIdConnectOptions)o)),
            ["oauth2"] = new(typeof(OAuthHandler<OAuthOptions>), typeof(OAuthOptions),         (ap, o) => ConfigureGenericOAuth2(ap, (OAuthOptions)o)),
        };

    public ProviderDescriptor ResolveDescriptor(string providerKey, AuthenticationProvider ap)
    {
        if (!string.IsNullOrEmpty(ap?.Type) && _genericTypes.TryGetValue(ap.Type, out var generic))
            return generic;
        if (_builtins.TryGetValue(providerKey, out var builtin))
            return builtin;
        throw new InvalidOperationException($"No descriptor for provider '{providerKey}' (Type='{ap?.Type}')");
    }

    public async Task<AuthenticationScheme> TryBuildSchemeAsync(string schemeName)
    {
        var (providerKey, clientId) = SchemeName.Split(schemeName);
        if (clientId == null) return null;

        var client = await LoadClientAsync(clientId);
        if (client?.AuthenticationProviders == null) return null;
        if (!client.AuthenticationProviders.TryGetValue(providerKey, out var ap) || ap == null) return null;
        if (string.IsNullOrEmpty(ap.ClientId)) return null;

        var desc = ResolveDescriptor(providerKey, ap);
        var display = ap.DisplayName ?? providerKey;
        return new AuthenticationScheme(schemeName, display, desc.HandlerType);
    }

    public async Task<AuthenticationProvider> GetEntryAsync(string providerKey, string clientId)
    {
        var client = await LoadClientAsync(clientId);
        if (client?.AuthenticationProviders == null) return null;
        client.AuthenticationProviders.TryGetValue(providerKey, out var ap);
        return ap;
    }

    private async Task<AppClient> LoadClientAsync(string clientId)
    {
        if (_clientCache.TryGetValue(clientId, out var cached)) return cached;
        var client = await _mongo.Filter<AppClient>()
            .Eq(x => x.ClientId, clientId)
            .FirstOrDefaultAsync();
        if (client != null) _clientCache.TryAdd(clientId, client);
        return client;
    }

    private static void ApplyScopes(ICollection<string> target, string[] scopes)
    {
        if (scopes == null) return;
        foreach (var s in scopes) target.Add(s);
    }

    private static void ConfigureGoogle(AuthenticationProvider ap, GoogleOptions o)
    {
        o.ClientId = ap.ClientId;
        o.ClientSecret = ap.ClientSecret;
        o.AccessType = "offline";
        if (ap.SaveTokens.HasValue) o.SaveTokens = ap.SaveTokens.Value;
        ApplyScopes(o.Scope, ap.Scopes);
    }

    private static void ConfigureMicrosoft(AuthenticationProvider ap, MicrosoftAccountOptions o)
    {
        o.ClientId = ap.ClientId;
        o.ClientSecret = ap.ClientSecret;
        if (ap.SaveTokens.HasValue) o.SaveTokens = ap.SaveTokens.Value;
        ApplyScopes(o.Scope, ap.Scopes);
    }

    private static void ConfigureGitHub(AuthenticationProvider ap, OAuthOptions o)
    {
        o.ClientId = ap.ClientId;
        o.ClientSecret = ap.ClientSecret;
        if (ap.SaveTokens.HasValue) o.SaveTokens = ap.SaveTokens.Value;
        o.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
        o.TokenEndpoint = "https://github.com/login/oauth/access_token";
        o.UserInformationEndpoint = "https://api.github.com/user";
        o.ClaimActions.MapJsonKey(JwtClaimTypes.Subject, "login");
        o.ClaimActions.MapJsonKey(JwtClaimTypes.Name, "name");
        o.ClaimActions.MapJsonKey("urn:github:id", "id");
        o.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");
        o.ClaimActions.MapJsonKey("urn:github:url", "html_url");
        ApplyScopes(o.Scope, ap.Scopes);
        o.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();
                using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                context.RunClaimActions(user.RootElement);
            }
        };
    }

    private static void ConfigureTypeform(AuthenticationProvider ap, OAuthOptions o)
    {
        o.ClientId = ap.ClientId;
        o.ClientSecret = ap.ClientSecret;
        if (ap.SaveTokens.HasValue) o.SaveTokens = ap.SaveTokens.Value;
        o.AuthorizationEndpoint = "https://api.typeform.com/oauth/authorize";
        o.TokenEndpoint = "https://api.typeform.com/oauth/token";
        o.UserInformationEndpoint = "https://api.typeform.com/me";
        ApplyScopes(o.Scope, ap.Scopes);
        o.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                var response = await context.Backchannel.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("user_id", out var uid))
                    context.Identity.AddClaim(new Claim(JwtClaimTypes.Subject, uid.GetString() ?? ""));
                if (root.TryGetProperty("alias", out var alias))
                    context.Identity.AddClaim(new Claim(JwtClaimTypes.Name, alias.GetString() ?? ""));
                if (root.TryGetProperty("email", out var email))
                    context.Identity.AddClaim(new Claim(JwtClaimTypes.Email, email.GetString() ?? ""));
            }
        };
    }

    private static void ConfigureOkta(AuthenticationProvider ap, OpenIdConnectOptions o)
    {
        o.RequireHttpsMetadata = true;
        o.ResponseType = OpenIdConnectResponseType.CodeIdToken;
        if (ap.SaveTokens.HasValue) o.SaveTokens = ap.SaveTokens.Value;
        o.GetClaimsFromUserInfoEndpoint = true;
        o.ClientId = ap.ClientId;
        o.ClientSecret = ap.ClientSecret;
        o.Authority = ap.Authority;
        ApplyScopes(o.Scope, ap.Scopes);
    }

    private static void ConfigureSalesforce(AuthenticationProvider ap, OpenIdConnectOptions o)
    {
        o.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
        o.NonceCookie.SameSite = SameSiteMode.None;
        o.NonceCookie.HttpOnly = true;
        o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        o.CorrelationCookie.SameSite = SameSiteMode.None;
        o.CorrelationCookie.HttpOnly = true;
        o.RequireHttpsMetadata = true;
        o.ResponseType = OpenIdConnectResponseType.Code;
        o.SaveTokens = ap.SaveTokens ?? true;
        o.ClientId = ap.ClientId;
        o.ClientSecret = ap.ClientSecret;
        o.Authority = ap.Authority;
        ApplyScopes(o.Scope, ap.Scopes);
        o.Events = new OpenIdConnectEvents
        {
            OnRemoteFailure = context =>
            {
                context.Response.Redirect("/Home/Error?message=" + context.Failure?.Message);
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    }

    private static void ConfigureGenericOidc(AuthenticationProvider ap, OpenIdConnectOptions o)
    {
        o.RequireHttpsMetadata = true;
        o.ResponseType = OpenIdConnectResponseType.Code;
        if (ap.SaveTokens.HasValue) o.SaveTokens = ap.SaveTokens.Value;
        o.GetClaimsFromUserInfoEndpoint = true;
        o.ClientId = ap.ClientId;
        o.ClientSecret = ap.ClientSecret;
        o.Authority = ap.Authority;
        ApplyScopes(o.Scope, ap.Scopes);
        ApplyClaimMappings(o.ClaimActions, ap);
    }

    private static void ConfigureGenericOAuth2(AuthenticationProvider ap, OAuthOptions o)
    {
        o.ClientId = ap.ClientId;
        o.ClientSecret = ap.ClientSecret;
        if (ap.SaveTokens.HasValue) o.SaveTokens = ap.SaveTokens.Value;
        o.AuthorizationEndpoint = ap.AuthorizationEndpoint;
        o.TokenEndpoint = ap.TokenEndpoint;
        o.UserInformationEndpoint = ap.UserInformationEndpoint;
        ApplyScopes(o.Scope, ap.Scopes);
        ApplyClaimMappings(o.ClaimActions, ap);

        if (!string.IsNullOrEmpty(o.UserInformationEndpoint))
        {
            o.Events = new OAuthEvents
            {
                OnCreatingTicket = async context =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();
                    using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    context.RunClaimActions(user.RootElement);
                }
            };
        }
    }

    private static void ApplyClaimMappings(ClaimActionCollection target, AuthenticationProvider ap)
    {
        if (!string.IsNullOrEmpty(ap.SubjectClaim)) target.MapJsonKey(JwtClaimTypes.Subject, ap.SubjectClaim);
        if (!string.IsNullOrEmpty(ap.EmailClaim))   target.MapJsonKey(JwtClaimTypes.Email,   ap.EmailClaim);
        if (!string.IsNullOrEmpty(ap.NameClaim))    target.MapJsonKey(JwtClaimTypes.Name,    ap.NameClaim);
        if (ap.ClaimMappings == null) return;
        foreach (var (claimType, jsonKey) in ap.ClaimMappings)
        {
            if (string.IsNullOrEmpty(claimType) || string.IsNullOrEmpty(jsonKey)) continue;
            target.MapJsonKey(claimType, jsonKey);
        }
    }
}
