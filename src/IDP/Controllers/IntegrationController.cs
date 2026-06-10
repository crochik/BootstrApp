using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;
using User = PI.Shared.Models.User;

namespace IDP.Controllers;

[Route("[controller]")]
public class IntegrationController : ControllerBase
{
    private readonly ILogger<IntegrationController> _logger;
    private readonly IConfiguration _configuration;
    private readonly MongoConnection _connection;
    private readonly AuthorizationService _authorizationService;
    private readonly SalesforceService _salesforceService;

    public IntegrationController(
        ILogger<IntegrationController> logger,
        IConfiguration configuration,
        MongoConnection connection,
        AuthorizationService authorizationService,
        SalesforceService salesforceService
    )
    {
        _logger = logger;
        _configuration = configuration;
        _connection = connection;
        _authorizationService = authorizationService;
        _salesforceService = salesforceService;
    }

    [AllowAnonymous]
    [HttpPost("Salesforce")]
    public async Task<IActionResult> SalesforceAsync([FromQuery] string clientId, [FromQuery] Guid accountId)
    {
        using var scope1 = _logger.AddScope(new
        {
            ClientId = clientId,
            AccountId = accountId,
        });

        _logger.LogInformation("Try to initiate session using Salesforce token");

        var idpConfig = _configuration.GetSection("Authentication:Salesforce").Get<ServicesExtensions.OpenIdConfig>();
        // var account = await _connection.Filter<Entity, Account>()
        //     .Eq(x => x.Id, accountId)
        //     .Eq(x => x.IsActive, true)
        //     .FirstOrDefaultAsync();

        var tokens = Request.Headers.Authorization.FirstOrDefault()?.Split(" ");
        if (tokens?.Length != 2 || tokens[0] != "Bearer") throw new ForbiddenException("Invalid Token");
        var schemeAndHost = idpConfig.Authority;
        var accessToken = tokens[1];

        var userInfo = await _salesforceService.GetUserInfoAsync(accessToken, schemeAndHost);
        if (userInfo == null)
        {
            _logger.LogError("Did not get user info from Salesforce");
            throw new NotAuthorizedException();
        }

        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.IsActive, true)
            .Eq(x => x.AccountId, accountId)
            .In(x => x.UserRoleId, new[] { nameof(EntityRoleId.User), nameof(EntityRoleId.Manager), nameof(EntityRoleId.Admin) })
            .ElemMatchBuilder(
                x => x.Identities,
                q => q
                    .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Salesforce))
                    .Eq(x => x.ExternalId, userInfo.UserId)
            ).FirstOrDefaultAsync();

        var result = await _authorizationService.GenerateJwtTokenAsync(user, clientId, clientCheck: idpClient => idpClient.Properties?.Any(x => x.Key == "SalesforceLogin" && x.Value == "true") ?? false);
        if (!result.IsSuccess)
        {
            throw new ForbiddenException(result.Status);
        }
        
        return Ok(result.Value);
    }
    
    public class InitRequest
    {
        [BindProperty(Name = "signed_request")]
        public string SignedRequest { get; set; }
    }
}