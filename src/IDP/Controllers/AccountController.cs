using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Crochik.Mongo;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IDP.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;
using PI.Shared.Services;
using Services;

/*
To allow IDP Controllers to use authentication:
        [Authorize("root", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
*/
namespace IDP.Controllers;

/// <summary>
/// Account Endpoint to handle user login/logout
/// used code from SignInManager (https://github.com/aspnet/Identity/blob/master/src/Identity/SignInManager.cs)
/// </summary>
[Route("[controller]/[action]")]
public class AccountController : Controller
{
    private const string LoginProviderKey = "LoginProvider";
    private const string XsrfKey = "XsrfId";
    private readonly ILogger<AccountController> _logger;
    private readonly MongoConnection _connection;
    private readonly LoginService _loginService;
    private readonly AuthorizationService _authorizationService;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IAuthenticationSchemeProvider _schemes;
    private readonly IDP.ProviderResolver _providerResolver;

    public AccountController(
        ILogger<AccountController> logger,
        MongoConnection connection,
        LoginService loginService,
        AuthorizationService authorizationService,
        IIdentityServerInteractionService interaction,
        IAuthenticationSchemeProvider schemes,
        IDP.ProviderResolver providerResolver
    )
    {
        _logger = logger;
        _connection = connection;
        _loginService = loginService;
        _authorizationService = authorizationService;
        _interaction = interaction;
        _schemes = schemes;
        _providerResolver = providerResolver;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string returnUrl = null, string provider = null)
    {
        // Clear the existing external cookie to ensure a clean login process
        // await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await HttpContext.SignOutAsync(Consts.ExternalCookieAuthenticationScheme);

        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        if (context == null)
        {
            _logger.LogError("Missing return url, can't do anything");
            return RedirectToLocal("/loginerror.html?error=missing_parameter");
        }

        _logger.LogInformation("Login: {ClientId} {Idp}", context.Client.ClientId, context.IdP);

        provider ??= context.IdP;

        if (string.IsNullOrEmpty(provider) || provider == "*")
        {
            // return RedirectToLocal("/index.html?returnUrl=" + WebUtility.UrlEncode(returnUrl));
            return await PickProviderAsync(context, returnUrl);
        }

        if (provider == Consts.AnonymousAuthenticationScheme)
        {
            return await LoginAnonymouslyAsync(context, returnUrl);
        }

        // Per-AppClient opt-in: if AuthenticationProviders[provider].ClientId is set, route through
        // the composite "Provider:ClientId" scheme so PerClientOptionsFactory resolves credentials
        // from the AppClient document. Otherwise challenge the bare scheme (existing global path).
        var apEntry = await _providerResolver.GetEntryAsync(provider, context.Client.ClientId);
        var schemeName = !string.IsNullOrEmpty(apEntry?.ClientId)
            ? IDP.SchemeName.Compose(provider, context.Client.ClientId)
            : provider;

        // Request a redirect to the external login provider.
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), new { returnUrl, provider = schemeName });
        var properties = ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, schemeName);
    }

    [HttpPost("/account/login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWithCode(string returnUrl, string provider, string code, [FromServices] MagicCodeService service)
    {
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        if (context == null)
        {
            _logger.LogError("Missing return url, can't do anything");
            return RedirectToLocal("/loginerror.html");
        }

        if (provider != "MagicCode")
        {
            _logger.LogError("Invalid {Provider}", provider);
            return RedirectToLocal("/loginerror.html");
        }

        var result = await service.GetAndValidateAsync(code, context.Client.ClientId);
        if (!result.IsSuccess) return RedirectToLocal("/loginerror.html");

        var profile = await _authorizationService.GetProfileAsync(result.Value.User, result.Value.Client);
        if (profile == null)
        {
            _logger.LogError("Couldn't resolve profile");
            return RedirectToLocal("/loginerror.html");
        }

        var userPrincipal = GeneratePrincipal("MagicCode", result.Value.User, null);
        await HttpContext.SignInAsync(Consts.DefaultAuthenticationScheme, userPrincipal, new AuthenticationProperties { IsPersistent = true });
        return RedirectToLocal(returnUrl);
    }

    private async Task<IActionResult> PickProviderAsync(AuthorizationRequest context, string returnUrl)
    {
        // remove acr_values from return url
        var parts = returnUrl.Split("?");
        var query = QueryHelpers.ParseQuery(parts[1]);
        query.Remove("acr_values");
        returnUrl = QueryHelpers.AddQueryString(parts[0], query);

        _logger.LogInformation("Redirect to login page: {Idp}", context.IdP);

        var client = await _connection.Filter<AppClient>().Eq(x => x.ClientId, context.Client.ClientId).FirstOrDefaultAsync();
        if (client == null)
        {
            return RedirectToLocal("/loginerror.html?error=invalid_client");
        }

        var entries = client.AuthenticationProviders
            .Select(kv => new PickProviderViewModel.ProviderEntry
            {
                Key = kv.Key,
                DisplayName = kv.Value?.DisplayName ?? kv.Key,
                Type = kv.Value?.Type,
            })
            .ToList();

        return View("PickProvider", new PickProviderViewModel
        {
            ReturnUrl = returnUrl,
            Providers = entries,
        });
    }

    /// <summary>
    /// HACK Create session for the anonymous user if the client supports it
    /// TODO: should move the code to the LoginService?
    /// ....
    /// </summary>
    private async Task<IActionResult> LoginAnonymouslyAsync(AuthorizationRequest context, string returnUrl)
    {
        var client = await _connection.Filter<AppClient>()
            .Eq(x => x.ClientId, context.Client.ClientId)
            .Ne(x => x.AccountId, null)
            .Ne(x => x.AnonymousUserId, null)
            .Ne(x => x.Enabled, false)
            .FirstOrDefaultAsync();

        if (client == null) return BadRequest("Invalid Client");

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, client.AccountId.Value)
            .Eq(x => x.Id, client.AnonymousUserId.Value)
            .Ne(x => x.IsActive, false)
            .Eq(x => x.UserRoleId, nameof(EntityRoleId.Profile))
            .FirstOrDefaultAsync();

        if (user == null) return BadRequest("Invalid configuration: user");

        var profile = await _authorizationService.GetProfileAsync(user, client);
        if (profile==null)
        {
            return BadRequest("Invalid configuration: profile");
        }

        var userPrincipal = GeneratePrincipal(context.IdP, user, null);
        await HttpContext.SignInAsync(Consts.DefaultAuthenticationScheme, userPrincipal, new AuthenticationProperties { IsPersistent = true });
        return RedirectToLocal(returnUrl);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(
        string provider,
        string returnUrl = null,
        string remoteError = null
    )
    {
        if (remoteError != null)
        {
            _logger.LogError("Error from external provider: {error}", remoteError);
            return Forbid();
        }

        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        if (context != null)
        {
            // ...
        }

        var info = await GetExternalLoginInfoAsync(provider);
        if (info == null)
        {
            return Forbid();
        }

        // Probe for ambiguous identity matches; if 2+ active users share this external identity,
        // render the picker. The External cookie is intentionally left intact so the picker POST
        // can re-read the same login info via GetExternalLoginInfoAsync.
        var lookup = await _loginService.FindLoginCandidatesAsync(info, context?.Client.ClientId);
        if (lookup?.Candidates?.Count > 1)
        {
            return View("PickUser", new PickUserViewModel
            {
                ReturnUrl = returnUrl,
                Provider = provider,
                Users = lookup.Candidates
                    .Select(c => new PickUserViewModel.UserEntry
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Email = c.Email,
                        Description = c.Description,
                    })
                    .ToList(),
            });
        }

        var login = await SignInAsync(context, info);
        if (login?.User != null)
        {
            if (returnUrl == null)
            {
                return Ok($"User: {login.User.Name} ({login.User.UserRoleId})");
            }

            return RedirectToLocal(returnUrl);
        }

        return RedirectToLocal("/loginerror.html");
    }

    [HttpPost("/account/PickUser")]
    [AllowAnonymous]
    public async Task<IActionResult> PickUser(PickUserInputModel input)
    {
        if (input == null || string.IsNullOrEmpty(input.ReturnUrl) || string.IsNullOrEmpty(input.Provider) || input.SelectedUserId == Guid.Empty)
        {
            return RedirectToLocal("/loginerror.html");
        }

        var context = await _interaction.GetAuthorizationContextAsync(input.ReturnUrl);
        if (context == null)
        {
            _logger.LogError("PickUser: missing return url context");
            return RedirectToLocal("/loginerror.html");
        }

        var info = await GetExternalLoginInfoAsync(input.Provider);
        if (info == null)
        {
            // External cookie expired between picker render and submit — restart the flow.
            _logger.LogWarning("PickUser: external login info not available; restarting");
            return Redirect($"/account/login?returnUrl={Uri.EscapeDataString(input.ReturnUrl)}");
        }

        await HttpContext.SignOutAsync(Consts.ExternalCookieAuthenticationScheme);

        var login = await _loginService.LoginUserAsync(info, context.Client.ClientId, context.AcrValues, input.SelectedUserId);
        if (login?.User == null)
        {
            return RedirectToLocal("/loginerror.html");
        }

        var userPrincipal = GeneratePrincipal(info.LoginProvider, login.User, login.Impersonator);
        await HttpContext.SignInAsync(Consts.DefaultAuthenticationScheme, userPrincipal, new AuthenticationProperties { IsPersistent = true });

        return RedirectToLocal(input.ReturnUrl);
    }

    [HttpGet]
    public async Task<IActionResult> Logout(string logoutId = null)
    {
        string redirectUrl = null;
        if (logoutId != null)
        {
            var context = await _interaction.GetLogoutContextAsync(logoutId);
            redirectUrl = context?.PostLogoutRedirectUri;
        }

        await HttpContext.SignOutAsync();
        _logger.LogInformation("User logged out.");

        if (redirectUrl != null)
        {
            _logger.LogInformation("Redirect to {url}", redirectUrl);
            return Redirect(redirectUrl);
        }

        return View("LoggedOut");
    }

    private IActionResult RedirectToLocal(string returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        throw new Exception($"Invalid RedirectUrl: {returnUrl}");
    }

    private async Task<LoginResult> SignInAsync(AuthorizationRequest context, ExternalLoginInfo loginInfo)
    {
        await HttpContext.SignOutAsync(Consts.ExternalCookieAuthenticationScheme);

        var login = await _loginService.LoginUserAsync(loginInfo, context?.Client.ClientId, context?.AcrValues);
        if (login == null) return null;

        var userPrincipal = GeneratePrincipal(loginInfo.LoginProvider, login.User, login.Impersonator);

        await HttpContext.SignInAsync(Consts.DefaultAuthenticationScheme, userPrincipal, new AuthenticationProperties { IsPersistent = true });

        return login;
    }

    /// <summary>
    /// Generate principal for user 
    /// see https://github.com/aspnet/AspNetCore/blob/master/src/Identity/Extensions.Core/src/UserClaimsPrincipalFactory.cs
    /// </summary>
    /// <param name="authenticationType"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    private ClaimsPrincipal GeneratePrincipal(string authenticationType, User user, User impersonator)
    {
        var id = new ClaimsIdentity(Consts.DefaultAuthenticationScheme, JwtClaimTypes.Name, JwtClaimTypes.Role);
        id.AddClaim(new Claim(JwtClaimTypes.Subject, user.Id.ToString()));
        id.AddClaim(new Claim(JwtClaimTypes.PreferredUserName, user.Id.ToString())); // ???
        id.AddClaim(new Claim(JwtClaimTypes.Name, user.Name));

        if (impersonator != null)
        {
            id.AddClaim(new Claim("pi_impersonator_id", impersonator.Id.ToString()));
        }

        // W/O THIS, identityserver will try to login again
        id.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, authenticationType));

        return new ClaimsPrincipal(id);
    }

    /// <summary>
    /// Configures the redirect URL and user identifier for the specified external login <paramref name="provider"/>.
    /// </summary>
    /// <param name="provider">The provider to configure.</param>
    /// <param name="redirectUrl">The external login URL users should be redirected to during the login flow.</param>
    /// <param name="userId">The current user's identifier, which will be used to provide CSRF protection.</param>
    /// <returns>A configured <see cref="AuthenticationProperties"/>.</returns>
    private AuthenticationProperties ConfigureExternalAuthenticationProperties(string provider, string redirectUrl, string userId = null)
    {
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        properties.Items[LoginProviderKey] = provider;
        if (userId != null)
        {
            properties.Items[XsrfKey] = userId;
        }

        return properties;
    }

    private async Task<ExternalLoginInfo> GetExternalLoginInfoAsync(string provider)
    {
        // var auth = await HttpContext.AuthenticateAsync(Consts.ExternalCookieAuthenticationScheme); // IdentityConstants.ExternalScheme);
        // if (auth == null || !auth.Succeeded)
        // {
        //     auth = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        // }
        var auth = await HttpContext.AuthenticateAsync(provider);

        var items = auth?.Properties?.Items;
        if (auth?.Principal == null || items == null || !items.ContainsKey(LoginProviderKey))
        {
            return null;
        }

        var providerKey = auth.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (providerKey == null)
        {
            providerKey = auth.Principal.FindFirstValue(JwtClaimTypes.Subject);
        }

        if (providerKey == null || provider == null)
        {
            return null;
        }

        var providerDisplayName = (await GetExternalAuthenticationSchemesAsync()).FirstOrDefault(p => p.Name == provider)?.DisplayName ?? provider;

        // Strip the per-client suffix so downstream (LoginService Mongo lookup, amr claim,
        // EntityIdentity.IdentityProviderId persistence) sees the bare provider name.
        var (bareProvider, _) = IDP.SchemeName.Split(provider);

        return new ExternalLoginInfo(auth.Principal, bareProvider, providerKey, providerDisplayName)
        {
            AuthenticationTokens = auth.Properties.GetTokens()
        };
    }

    /// <summary>
    /// Gets a collection of <see cref="AuthenticationScheme"/>s for the known external login providers.		
    /// </summary>		
    /// <returns>A collection of <see cref="AuthenticationScheme"/>s for the known external login providers.</returns>		
    private async Task<IEnumerable<AuthenticationScheme>> GetExternalAuthenticationSchemesAsync()
    {
        var schemes = await _schemes.GetAllSchemesAsync();
        return schemes.Where(s => !string.IsNullOrEmpty(s.DisplayName));
    }
}