using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Events;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IDP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IDP.Controllers;

[Authorize]
public class ConsentController(
    IIdentityServerInteractionService interaction,
    IEventService events,
    ILogger<ConsentController> logger)
    : Controller
{
    private readonly ILogger<ConsentController> _logger = logger;

    [HttpGet]
    public async Task<IActionResult> Index(string returnUrl)
    {
        var vm = await BuildViewModelAsync(returnUrl);
        if (vm == null) return View("Error");
        return View("Index", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ConsentInputModel model)
    {
        var request = await interaction.GetAuthorizationContextAsync(model.ReturnUrl);
        if (request == null) return View("Error");

        ConsentResponse grantedConsent = null;

        if (model.Button == "no")
        {
            grantedConsent = new ConsentResponse 
            { 
                Error = AuthorizationError.AccessDenied 
            };
            await events.RaiseAsync(new ConsentDeniedEvent(
                User.GetSubjectId(), request.Client.ClientId, request.ValidatedResources.RawScopeValues));
        }
        else if (model.Button == "yes")
        {
            if (model.ScopesConsented != null && model.ScopesConsented.Any())
            {
                var scopes = model.ScopesConsented;
                grantedConsent = new ConsentResponse
                {
                    RememberConsent = model.RememberConsent,
                    ScopesValuesConsented = scopes.ToArray(),
                    Description = model.Description
                };
                await events.RaiseAsync(new ConsentGrantedEvent(
                    User.GetSubjectId(), request.Client.ClientId, 
                    request.ValidatedResources.RawScopeValues,
                    grantedConsent.ScopesValuesConsented, 
                    grantedConsent.RememberConsent));
            }
            else
            {
                ModelState.AddModelError(string.Empty, "You must pick at least one permission.");
            }
        }

        if (grantedConsent != null)
        {
            await interaction.GrantConsentAsync(request, grantedConsent);
            return Redirect(model.ReturnUrl);
        }

        // Re-show the form with errors
        var vm = await BuildViewModelAsync(model.ReturnUrl, model);
        return View("Index", vm);
    }

    private async Task<ConsentViewModel> BuildViewModelAsync(string returnUrl, ConsentInputModel model = null)
    {
        var request = await interaction.GetAuthorizationContextAsync(returnUrl);
        if (request == null) return null;

        return new ConsentViewModel
        {
            RememberConsent = model?.RememberConsent ?? true,
            ScopesConsented = model?.ScopesConsented ?? Enumerable.Empty<string>(),
            Description = model?.Description,
            ReturnUrl = returnUrl,
            ClientName = request.Client.ClientName ?? request.Client.ClientId,
            ClientUrl = request.Client.ClientUri,
            ClientLogoUrl = request.Client.LogoUri,
            AllowRememberConsent = request.Client.AllowRememberConsent,
            IdentityScopes = request.ValidatedResources.Resources.IdentityResources
                .Select(x => CreateScopeViewModel(x, model?.ScopesConsented?.Contains(x.Name) ?? true)),
            ApiScopes = request.ValidatedResources.Resources.ApiScopes
                .Select(x => CreateScopeViewModel(x, model?.ScopesConsented?.Contains(x.Name) ?? true))
        };
    }

    private ScopeViewModel CreateScopeViewModel(IdentityResource identity, bool check)
    {
        return new ScopeViewModel
        {
            Value = identity.Name,
            DisplayName = identity.DisplayName ?? identity.Name,
            Description = identity.Description,
            Emphasize = identity.Emphasize,
            Required = identity.Required,
            Checked = check || identity.Required
        };
    }

    private ScopeViewModel CreateScopeViewModel(ApiScope scope, bool check)
    {
        return new ScopeViewModel
        {
            Value = scope.Name,
            DisplayName = scope.DisplayName ?? scope.Name,
            Description = scope.Description,
            Emphasize = scope.Emphasize,
            Required = scope.Required,
            Checked = check || scope.Required
        };
    }
}

