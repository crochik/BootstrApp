using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Openphone.Models;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.Openphone.Controllers;

[Route("/openphone/v1/[controller]")]
public class WebhookController : APIController
{
    private readonly ILogger<WebhookController> _logger;
    private readonly MongoConnection _connection;
    private Dictionary<string, IEventHandler> _eventHandlers; 

    public WebhookController(ILogger<WebhookController> logger, MongoConnection connection, ObjectTypeService objectTypeService, IEnumerable<IEventHandler> handlers)
    {
        _logger = logger;
        _connection = connection;
        _eventHandlers = handlers.ToDictionary(x => x.ObjectType);
    }

    [HttpPost("/openphone/v1/[controller]({entityId})")]
    public async Task<IActionResult> AddEventAsync([FromRoute] Guid entityId, [FromBody] OpenPhoneRawEvent request)
    {
        using var scope = _logger.AddScope(new
        {
            request.Type,
            request.Data?.From,
            request.Data?.To,
            request.Data?.Direction,
            request.Data?.Status,
            request.Data?.FullNameWithCompany,
            EntityId = entityId,
        });

        _logger.LogInformation("Received Event");

        var organization = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.Id, entityId)
            .Eq(x => x.IsActive, true)
            .FirstOrDefaultAsync();

        if (organization == null) throw new ForbiddenException("Organization");

        var integration = await _connection.Filter<OpenPhoneIntegrationConfiguration>()
            .Eq(x => x.AccountId, organization.AccountId)
            .Eq(x => x.EntityId, organization.Id)
            .Eq(x => x.IntegrationId, IntegrationIds.OpenPhone)
            .FirstOrDefaultAsync();

        if (integration == null) throw new ForbiddenException("Integration");
       
        // TODO: implement signature check
        // https://support.openphone.com/hc/en-us/articles/4690754298903-How-to-use-webhooks#h_01HHNEAY9S70855QD5E639XRF2
        // hmac;1;1719404801757;Xukfe1Ejluvy1rcNHxF4+zugrXs+1lVGARhBpm/N3C0=
        if (!Request.Headers.TryGetValue("OpenPhone-Signature", out var signatureHeaders) || signatureHeaders.Count != 1) throw new ForbiddenException();
        var signatureParts = signatureHeaders.FirstOrDefault()?.Split(';');
        if (signatureParts?.Length != 4 || signatureParts[0] != "hmac" || signatureParts[1] != "1") throw new ForbiddenException();
        var timestamp = signatureParts[2];
        var signature = signatureParts[3];
        // ...

        var evt = new OpenPhoneEvent
        {
            Id = Guid.NewGuid(),
            AccountId = AccountIds.FCI,
            EntityId = entityId,
            CreatedOn = DateTime.UtcNow,
            ExternalId = request.Id.StartsWith("EV") ? request.Id[2..] : request.Id,
            Name = request.Name,
            Description = request.Description,
            Event = request,
        };

        foreach (var header in Request.Headers)
        {
            evt.Headers.Add(header.Key, header.Value.Count > 1 ? string.Join(", ", header.Value) : header.Value.FirstOrDefault());
        }

        // TODO: find object type based on the object
        // get object type 
        // set flow, object status
        // ...

        var update = _connection.Filter<OpenPhoneEvent>()
                .Eq(x => x.AccountId, evt.AccountId)
                .Eq(x => x.EntityId, evt.EntityId)
                .Eq(x => x.ExternalId, evt.ExternalId)
                .Update
                .SetOnInsert(x => x.Id, Guid.NewGuid())
                .SetOnInsert(x => x.AccountId, evt.AccountId)
                .SetOnInsert(x => x.EntityId, evt.EntityId)
                .SetOnInsert(x => x.CreatedOn, request.CreatedAt)
                .SetOnInsert(x => x.ExternalId, evt.ExternalId)
                .SetOnInsert(x => x.Headers, evt.Headers)
                .SetOnInsert(x => x.Event, request)
                .SetOnInsert(x => x.Name, evt.Name)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            ;

        if (evt.Description != null)
        {
            update.SetOnInsert(x => x.Description, evt.Description);
        }

        var phoneNumber = request.PhoneNumber;
        if (PhoneNumber.TryParse(phoneNumber, out var ph))
        {
            evt.NormalizedPhoneNumber = ph.International;
            evt.Phone = ph.Display;

            update.SetOnInsert(x => x.Phone, evt.Phone)
                .SetOnInsert(x => x.NormalizedPhoneNumber, evt.NormalizedPhoneNumber);
        }

        var result = await update.UpdateOneAsync(true);
        if (result.UpsertedId == null) return Ok();

        if (!evt.Event.Data.ObjectProperties.TryGetStrParam("object", out var opObjectType))
        {
            _logger.LogError("Couldn't get object type");
            return NotFound();
        }
        
        if (_eventHandlers.TryGetValue(opObjectType, out var handler))
        {
            _logger.LogInformation("Handle {Object}", opObjectType);
            await handler.HandleAsync(organization, integration, evt);
        }
        else
        {
            _logger.LogError("Nothing to handle for {Object}", opObjectType);
        }
        
        return Created(Request.GetDisplayUrl(), request.Id);
    }
}