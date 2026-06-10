using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.O365;
using PI.Shared.Services;

namespace Controllers;

[Route("/o365/v1/[controller]")]
public class AdminController : APIController
{
    private readonly ILogger<AdminController> _logger;
    private readonly MongoConnection _connection;
    private readonly O365AuthClient _client;

    public AdminController(
        ILogger<AdminController> logger,
        MongoConnection connection,
        O365AuthClient client
    )
    {
        _logger = logger;
        _connection = connection;
        _client = client;
    }

    // https://login.microsoftonline.com/8842b3ae-07b8-4333-98f9-c80b1308d0a4/v2.0/adminconsent
    // ?client_id=8b6de780-0e2b-4564-a071-85b472840b5d
    // &redirect_uri=https://api.fci.cloud/o365/v1/Admin/EnableAdmin/Callback
    // &state=fc100000-0000-0000-0000-000000000000
    // &scope=https://graph.microsoft.com/.default
    [HttpGet("EnableAdmin")]
    public async Task<IActionResult> EnableAdminAsync()
    {
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.Id, Context.EntityId.Value)
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.UserRoleId, EntityRoleId.Admin.ToString())
            .FirstOrDefaultAsync();

        if (user == null) throw new ForbiddenException();

        var identity = user.Identities.FirstOrDefault(x => x.IdentityProviderId.ToString() == ExternalProvider.Microsoft.ToString());
        if (identity == null) throw new BadRequestException("No Microsoft Identity");

        var account = await _connection.Filter<Entity, Account>()
            .Eq(x => x.Id, user.AccountId)
            .FirstOrDefaultAsync();

        var accountIdentity = account?.FirstIdentity(ExternalProvider.Microsoft);
        if (accountIdentity == null) throw new NotFoundException(nameof(Account), user.AccountId);

        var state = account.Id; // ????
        var tenant = accountIdentity.ExternalId;
        var clientId = _client.Config.ClientId;
        var redirectUrl = new UriBuilder
        {
            Scheme = Request.Scheme,
            Host = Request.Host.Host,
            Port = Request.Host.Port ?? 443,
            Path = Url.Action("EnableAdminCallback")
        }.ToString();

        return Ok(
            new
            {
                Url = $"https://login.microsoftonline.com/{tenant}/v2.0/adminconsent?client_id={clientId}&redirect_uri={redirectUrl}&state={state}&scope=https://graph.microsoft.com/.default"
            }
        );
    }

    // http://localhost:5000/microsoft/enable-admin?tenant=66e4ad38-89b1-42da-9252-cfef787fa221
    [HttpGet("EnableAdmin/Callback")]
    [AllowAnonymous]
    public async Task<IActionResult> EnableAdminCallbackAsync(
        string tenant,
        string state,
        bool admin_consent = false)
    {
        if (!admin_consent)
        {
            _logger.LogError("Failed to get consent for {state}/{tenant}", state, tenant);
            return Redirect("/microsoft-error.html");
        }

        if (!Guid.TryParse(state, out var accountId))
        {
            _logger.LogError("Failed to parse accountId from {state}/{tenant}", state, tenant);
            return Redirect("/microsoft-error.html");
        }

        var result = await _client.UpdateTokenAsync(accountId, tenant);

        // TODO: use parameter from client or some standard page that will close itself
        // ...
        return Redirect(result ?
            "/microsoft-success.html" :
            "/microsoft-error.html"
        );
    }
}