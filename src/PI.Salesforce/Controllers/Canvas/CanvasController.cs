using System;
using System.Text;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Models;
using PI.Shared.Salesforce.Models.Canvas;
using PI.Shared.Services;

namespace Controllers;

[Route("/salesforce/v1/[controller]")]
public class CanvasController : ControllerBase
{
    private readonly MongoConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly SalesforceService _salesforceService;

    public CanvasController(
        MongoConnection connection,
        IServiceProvider serviceProvider,
        SalesforceService salesforceService
    )
    {
        _connection = connection;
        _serviceProvider = serviceProvider;
        _salesforceService = salesforceService;
    }

    // https://github.com/short000/salesforce-canvas-dotnet
    // https://d1i000001xzmxuas-dev-ed.my.salesforce.com/app/mgmt/forceconnectedapps/forceAppEdit.apexp?connectedAppId=0H48W000000sBYLSA2&appLayout=setup&noS1Redirect=true&id=0Ci8W0000008RWe
    // https://developer.salesforce.com/docs/atlas.en-us.platform_connect.meta/platform_connect/canvas_app_aura_code_example.htm
    [AllowAnonymous]
    [HttpPost]
    [HttpPost("{clientId}")]
    [HttpPost("{clientId}/{page}")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> InitAsync(InitRequest payload, [FromRoute] string clientId, [FromRoute] string page, [FromQuery] int? height)
    {
        var parts = payload.SignedRequest.Split(".");
        var valueBytes = System.Convert.FromBase64String(parts[1]);
        var json = Encoding.UTF8.GetString(valueBytes);

        var signedRequest = JsonConvert.DeserializeObject<SignedRequest>(json);

        // TODO: check signature
        // ...

        var userInfo = await _salesforceService.GetUserInfoAsync(signedRequest.Client.OauthToken, signedRequest.Context.Links.LoginUrl);
        if (userInfo == null) return error("Failed to get user info");

        var account = await _connection.Filter<Entity, Account>()
            .Eq(x => x.IsActive, true)
            .ElemMatchBuilder(
                x => x.Identities,
                q => q
                    .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Salesforce))
                    .Eq(x => x.ExternalId, userInfo.OrganizationId)
            ).FirstOrDefaultAsync();

        if (account == null) return error("Account is not setup.");

        var user = await _connection.Filter<Entity, PI.Shared.Models.User>()
            .Eq(x => x.IsActive, true)
            .Eq(x => x.AccountId, account.Id)
            .In(x => x.UserRoleId, new[] { nameof(EntityRoleId.User), nameof(EntityRoleId.Manager), nameof(EntityRoleId.Admin), nameof(EntityRoleId.Profile) })
            .ElemMatchBuilder(
                x => x.Identities,
                q => q
                    .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Salesforce))
                    .Eq(x => x.ExternalId, userInfo.UserId)
            ).FirstOrDefaultAsync();

        if (user == null) return error("Invalid User");

        clientId ??= "salesforce_canvas";

        var client = await _connection.Filter<AppClient>()
            .Eq(x => x.ClientId, clientId)
            .Eq(x => x.AccountId, account.Id)
            .Ne(x => x.Enabled, false)
            .ElemMatchBuilder(x => x.Properties, q => q
                .Eq(x => x.Key, "SalesforceLogin")
                .Eq(x => x.Value, "true")
            )
            .FirstOrDefaultAsync();

        if (client == null)
        {
            return error("Invalid client");
        }

        var record = signedRequest.Context.Environment.Record;
        var loader = GetObjectLoader(user.AccountId, record?.Attributes?.Type);

        var result = await loader.LoadAsync(signedRequest, user, client, record, page, height);
        return result.IsSuccess ? Redirect(result.Value) : error(result.Status);
        
        ContentResult error(string message)
        {
            var html = "<html>" +
                       "<head><title>Error</title></head>" +
                       "<body><div style='width: 300px; height: 300px; padding: 24px; background-color: #ffff00'><p><b>Error: </b>" + message + "</p></div></body>" +
                       "</html>";
            return Content(html, "text/html");
        }        
    }
    
    private IObjectLoader GetObjectLoader(Guid accountId, string sfObjectTypeName)
    {
        using IServiceScope serviceScope = _serviceProvider.CreateScope();
        return sfObjectTypeName switch
        {
            "Lead" => serviceScope.ServiceProvider.GetRequiredService<LeadPageLoader>(),
            "Account" => serviceScope.ServiceProvider.GetRequiredService<AccountPageLoader>(),
            "ServiceAppointment" => serviceScope.ServiceProvider.GetRequiredService<ServiceAppointmentPageLoader>(),
            "WorkOrder" => serviceScope.ServiceProvider.GetRequiredService<WorkOrderPageLoader>(),
            "INET_Option__c" => serviceScope.ServiceProvider.GetRequiredService<OptionPageLoader>(),
            _ => serviceScope.ServiceProvider.GetRequiredService<DefaultPageLoader>(),
        };
    }

    public class InitRequest
    {
        [BindProperty(Name = "signed_request")]
        public string SignedRequest { get; set; }
    }
}
