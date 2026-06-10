using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Salesforce.MarketingCloud;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Services.DataProtection;
using Services;

namespace Controllers;

[Authorize("admin")]
[Route("/marketingcloud/v1/[controller]")]
public class DataExtensionController : APIController
{
    private readonly MongoConnection _connection;

    public DataExtensionController(MongoConnection connection)
    {
        _connection = connection;
    }

    [HttpGet("Token")]
    public async Task<IActionResult> GetTokenAsync([FromServices] MarketingCloudClient client, [FromServices] DataProtectionService service)
    {
        var integration = await _connection.Filter<MarketingCloudIntegrationConfiguration>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.EntityId, Context.AccountId.Value)
            .Eq(x => x.IntegrationId, IntegrationIds.MarketingCloud) // redundant as we will limit by type
            .FirstOrDefaultAsync();

        if (integration == null) throw NotFoundException.New("Integration");

        var clientSecret = await service.UnprotectAsync(
            Context,
            new MicrosoftDataProtectionConfig
            {
                Purpose = MarketingCloudIntegrationConfiguration.ProtectionKey,
            },
            integration.ClientSecret
        );
        
        var token = await client.GetTokenAsync(integration.Subdomain, integration.ClientId, clientSecret);
        
        return token.AccessToken!=null ? Ok() : NotFound("Couldn't get token");
    }
}