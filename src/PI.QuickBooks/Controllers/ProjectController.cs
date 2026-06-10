using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.QuickBooks.Services;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace Controllers;

[Produces("application/json")]
[Route("/quickbooks/v1/[controller]")]
public class ProjectController : APIController
{
    private readonly MongoConnection _connection;
    private readonly QuickBooksService _service;

    public ProjectController(MongoConnection connection, QuickBooksService service)
    {
        _connection = connection;
        _service = service;
    }

    [Authorize("admin")]
    [HttpPost("/quickbooks/v1/Customer({customerId})/Project")]
    public async Task<Project> CreateProjectAsync([FromRoute] string customerId, [FromQuery] Guid? organizationId)
    {
        IEntityContext context = Context;
        if (organizationId.HasValue)
        {
            var org = await _connection.Filter<Entity, Organization>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Id, organizationId.Value)
                .FirstOrDefaultAsync();

            context = org.Context;
        }
        
        var integration = await _service.GetIntegrationAsync(context);
        if (integration == null) throw new BadRequestException("Integration not configured");

        var accessToken = await _service.GetAccessTokenAsync(context, integration);
        if (!accessToken.IsSuccess)
        {
            throw new BadRequestException(accessToken.Status);
        }

        var graphQl = new QuickBooksGraphQL();
        return await graphQl.CreateProjectAsync(accessToken.Value, integration.CompanyId, customerId);
    }
}