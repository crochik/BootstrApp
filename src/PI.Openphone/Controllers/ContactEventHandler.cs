using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Openphone.Models;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;

namespace PI.Openphone.Controllers;

public class ContactEventHandler : IEventHandler
{
    private readonly ILogger<ContactEventHandler> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    public string ObjectType => "contact";

    public ContactEventHandler(ILogger<ContactEventHandler> logger, MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
    }
    
    public async Task HandleAsync(Organization organization, OpenPhoneIntegrationConfiguration integration, OpenPhoneEvent evt)
    {
        if (!evt.Event.Data.ObjectProperties.TryGetStrParam("id", out var externalObjectId))
        {
            _logger.LogError("Couldn't resolve contact id");
            return;
        }
        
        using var scope = _logger.AddScope(new
        {
            OpenPhoneContactId = externalObjectId,
        });

        if (externalObjectId.StartsWith("CT"))
        {
            externalObjectId = externalObjectId[2..];
        }

        var orgContext = organization.Context;
        if (!integration.Events.TryGetValue("Contact", out var config))
        {
            config = null;
        }
        
        // TODO: get object type to use to calculate initial and calculated values
        //  ...
        // var objectType = await _objectTypeService.GetAsync(orgContext, config?.ObjectType ?? "openphone.Contact");

        var contact = await _connection.Filter<OpenPhoneContact>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.EntityId, evt.EntityId)
            .Eq(x => x.ExternalId, externalObjectId)
            .FirstOrDefaultAsync();

        var obj = default(object);
        if (!evt.Event.Data.ObjectProperties.TryResolvePathValue("fields|Phone", out obj) || obj is not string phone)
        {
            phone = null;
        }
        else
        {
            phone = Lead.GetNormalizedPhoneNumber(phone);
        }
        
        if (!evt.Event.Data.ObjectProperties.TryResolvePathValue("fields|Email", out obj) || obj is not string email)
        {
            email = null;
        }
        else
        {
            email = Lead.GetNormalizedEmail(email);
        }
        
        if (contact != null)
        {
            _logger.LogInformation("Update Contact");

            if (!evt.Event.Data.ObjectProperties.TryGetValue("updatedAt", out var updatedAtObj))
            {
                _logger.LogError("Couldn't find updatedAt");
                return;
            }

            if (updatedAtObj is not DateTime updatedAt)
            {
                _logger.LogError("Couldn't get updatedAt: {UpdatedAt} {UpdatedAtType}", updatedAtObj, updatedAtObj?.GetType().FullName);
                return;
            }
            
            // TODO: it seems that a note change will not change the updatedAt on the contact
            // and if so, we will not update the contact with the note-only-change
            // ...
            
            contact = await _connection.Filter<OpenPhoneContact>()
                .Eq(x => x.AccountId, evt.AccountId)
                .Eq(x => x.Id, contact.Id)
                .Lt(x => x.Properties["updatedAt"], updatedAt)
                .Update
                .Set(x => x.Properties, evt.Event.Data.ObjectProperties)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.Name, evt.Event.Data.FullNameWithCompany)
                .SetOrUnset(x => x.NormalizedEmail, email)
                .SetOrUnset(x => x.NormalizedPhoneNumber, phone)
                .Set(x => x.IsActive, evt.Event.Type != OpenPhoneRawEvent.ContactDeleted)
                .UpdateAndGetOneAsync();

            if (contact == null)
            {
                _logger.LogInformation("Didn't update, out of date notification");
                return;
            }

            if (!contact.IsActive)
            {
                // contact was deleted 
                // ...
            }

            await _objectTypeService.FireObjectUpdatedAsync(orgContext, contact, new Dictionary<string, object>
            {
                { nameof(OpenPhoneContact.Properties), "*" },
                { nameof(OpenPhoneContact.Name), contact.Name },
                { nameof(OpenPhoneContact.NormalizedEmail), contact.NormalizedEmail },
                { nameof(OpenPhoneContact.NormalizedPhoneNumber), contact.NormalizedPhoneNumber },
            });
            
            return;
        }

        // create
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var update = _connection.Filter<OpenPhoneContact>()
                .Eq(x => x.AccountId, evt.AccountId)
                .Eq(x => x.EntityId, evt.EntityId)
                .Eq(x => x.ExternalId, externalObjectId)
                .Update
                .SetOnInsert(x => x.AccountId, evt.AccountId)
                .SetOnInsert(x => x.EntityId, evt.EntityId)
                .SetOnInsert(x => x.ExternalId, externalObjectId)
                .SetOnInsert(x => x.Id, id)
                .SetOnInsert(x => x.CreatedOn, now)
                .SetOrUnset(x => x.NormalizedEmail, email)
                .SetOrUnset(x => x.NormalizedPhoneNumber, phone)
                .Set(x => x.Properties, evt.Event.Data.ObjectProperties)
                .Set(x => x.LastModifiedOn, now)
                .Set(x => x.Name, evt.Event.Data.FullNameWithCompany)
                .Set(x => x.IsActive, evt.Event.Type != OpenPhoneRawEvent.ContactDeleted)
            ;

        if (config != null)
        {
            if (config.FlowId.HasValue) update.SetOnInsert(x => x.FlowId, config.FlowId.Value);
            if (config.ObjectStatusId.HasValue) update.SetOnInsert(x => x.ObjectStatusId, config.ObjectStatusId.Value);
        }
            
        contact = await update.UpdateAndGetOneAsync(true);

        await _objectTypeService.FireCreateEventAsync(orgContext, contact);

        _logger.LogInformation("Contact added: {Id}", contact.Id);
    }
}