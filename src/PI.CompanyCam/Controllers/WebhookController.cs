using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.CompanyCam.Models;
using PI.CompanyCam.Services;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.CompanyCam.Controllers;

[Route("/companycam/v1/[controller]")]
public class WebhookController : APIController
{
    private readonly ILogger<WebhookController> _logger;
    private readonly MongoConnection _connection;
    private readonly CompanyCamService _service;

    public WebhookController(ILogger<WebhookController> logger, MongoConnection connection, CompanyCamService service)
    {
        _logger = logger;
        _connection = connection;
        _service = service;
    }
    

    [Authorize("manager")]
    [HttpGet("Subscription")]
    public async Task<IEnumerable<Webhook>> ListSubscriptionsAsync()
    {
        var client = await _service.GetClientAsync(Context);
        var list = await client.ListWebhooksAsync(null, null);
        return list;
    }

    [Authorize("manager")]
    [HttpDelete("Subscription")]
    public async Task<IEnumerable<Webhook>> RemoveSubscriptionsAsync()
    {
        var client = await _service.GetClientAsync(Context);
        var list = await client.ListWebhooksAsync(null, null);
        foreach (var item in list)
        {
            await client.DeleteWebhookAsync(item.Id);
        }

        list = await client.ListWebhooksAsync(null, null);

        return list;
    }

    [Authorize("manager")]
    [HttpPost("/companycam/v1/[controller]/Subscription")]
    public async Task<Webhook> SubscribeOrganizationAsync()
    {
        var result = await _service.CreateWebhookAsync(Context);
        if (!result.IsSuccess) throw new BadRequestException(result.Status);
        return result.Value;
    }

    [Authorize("admin")]
    [HttpGet("/companycam/v1/[controller]/Subscription/Entity({entityId})")]
    public async Task<IEnumerable<Webhook>> ListSubscriptionsAsync([FromRoute] Guid entityId)
    {
        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, entityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (entity == null) throw NotFoundException.New<Organization>(entityId);

        var client = await _service.GetClientAsync(entity.Context);
        var list = await client.ListWebhooksAsync(null, null);
        return list;
    }

    [Authorize("admin")]
    [HttpDelete("/companycam/v1/[controller]/Subscription/Entity({entityId})")]
    public async Task<IEnumerable<Webhook>> RemoveSubscriptionsAsync([FromRoute] Guid entityId)
    {
        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, entityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (entity == null) throw NotFoundException.New<Organization>(entityId);

        var client = await _service.GetClientAsync(entity.Context);
        var list = await client.ListWebhooksAsync(null, null);
        foreach (var item in list)
        {
            await client.DeleteWebhookAsync(item.Id);
        }

        list = await client.ListWebhooksAsync(null, null);

        return list;
    }
    
    [Authorize("admin")]
    [HttpPost("/companycam/v1/[controller]/Subscription/Entity({entityId})")]
    public async Task<Webhook> SubscribeAsync([FromRoute] Guid entityId)
    {
        var entity = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, entityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (entity == null) throw NotFoundException.New<Organization>(entityId);

        var result = await _service.CreateWebhookAsync(entity.Context);
        if (!result.IsSuccess) throw new BadRequestException(result.Status);
        return result.Value;
    }

    [AllowAnonymous]
    [HttpPost("/companycam/v1/[controller]({entityId})")]
    public async Task<IActionResult> AddEventAsync([FromRoute] Guid entityId, [FromBody] ExpandoObject body, [FromServices] ObjectTypeService objectTypeService)
    {
        _logger.LogInformation("Received {EntityId}: {JSON}", entityId, JsonConvert.SerializeObject(body));

        // TODO: check signature
        // ...

        var organization = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.Id, entityId)
            .Eq(x => x.IsActive, true)
            .FirstOrDefaultAsync();

        if (organization == null) throw new ForbiddenException("Organization");
        var integration = await _connection.Filter<CCIntegrationConfiguration>()
            .Eq(x => x.AccountId, organization.AccountId)
            .Eq(x => x.EntityId, organization.Id)
            .Eq(x => x.IntegrationId, IntegrationIds.CompanyCam)
            .FirstOrDefaultAsync();
        
        if (integration == null) throw new ForbiddenException("Integration");   

        var now = DateTime.UtcNow;
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            CreatedOn = now,
            AccountId = organization.AccountId,
            EntityId = organization.Id,
            Body = body,
            FlowId = integration.EventFlowId,
            ObjectStatusId = integration.EventObjectStatusId,
        };

        foreach (var header in Request.Headers)
        {
            evt.Headers.Add(header.Key, header.Value.Count > 1 ? string.Join(", ", header.Value) : header.Value.FirstOrDefault());
        }

        await _connection.InsertAsync(evt);

        await objectTypeService.FireCreateEventAsync(organization.Context, evt);           

        return Ok();
    }
}