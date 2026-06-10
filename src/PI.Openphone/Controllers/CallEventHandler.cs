using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Openphone.Models;
using PI.Shared.Models;

namespace PI.Openphone.Controllers;

public class CallEventHandler : IEventHandler
{
    public const string Provider = "OpenPhone";

    private readonly ILogger<CallEventHandler> _logger;
    private readonly MongoConnection _connection;
    public string ObjectType => "call";

    public CallEventHandler(ILogger<CallEventHandler> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public async Task HandleAsync(Organization organization, OpenPhoneIntegrationConfiguration integration, OpenPhoneEvent evt)
    {
        var raw = JsonConvert.DeserializeObject<OpenPhoneRawCall>(JsonConvert.SerializeObject(evt.Event.Data.Object));

        using var scope = _logger.AddScope(new
        {
            CallId = raw.Id,
        });

        _logger.LogInformation("Upsert Call");

        var externalObjectId = raw.Id;
        var context = organization.Context;
        if (!integration.Events.TryGetValue("Call", out var config))
        {
            config = null;
        }

        var from = Lead.GetNormalizedPhoneNumber(raw.From);
        var to = Lead.GetNormalizedPhoneNumber(raw.To);
        var direction = raw.Direction == "incoming" ? CommunicationDirection.Inbound : CommunicationDirection.Outbound;
        var phoneNumber = direction == CommunicationDirection.Inbound ? from : to;
        var now = DateTime.UtcNow;

        var id = Guid.NewGuid();
        var update = _connection.Filter<CommunicationNote>()
                .Eq(x => x.AccountId, organization.AccountId)
                .Eq(x => x.Provider, Provider)
                .Eq(x => x.ExternalId, externalObjectId)
                .Update
                .SetOnInsert("_t", "communication")
                .SetOnInsert(x => x.Id, id)
                .SetOnInsert(x => x.AccountId, organization.AccountId)
                .SetOnInsert(x => x.Provider, Provider)
                .SetOnInsert(x => x.ExternalId, externalObjectId)
                .SetOnInsert(x => x.EntityId, organization.Id)
                .SetOnInsert(x => x.Direction, direction)
                .SetOnInsert(x => x.CommunicationChannel, CommunicationChannel.Phone)
                .SetOnInsert(x => x.CreatedOn, raw.CreatedAt)
                .SetOnInsert(x => x.Refs, new List<KeyValuePair<string, object>>
                {
                    new("PhoneNumber", phoneNumber),
                    new("openphone|ConversationId", raw.ConversationId),
                    new("openphone|PhoneNumberId", raw.PhoneNumberId),
                    new("openphone|UserId", raw.UserId),
                })
                .SetOnInsert(x => x.Parties, new[]
                {
                    new CommunicationParty
                    {
                        Direction = CommunicationDirection.Outbound,
                        Address = from,
                    },
                    new CommunicationParty
                    {
                        Direction = CommunicationDirection.Inbound,
                        Address = to,
                    }
                })
                .Set(x => x.LastModifiedOn, now)
                .Set(x => x.LastActor, context.Actor())
                .Set(x => x.Status, raw.Status switch
                {
                    "ringing" => CommunicationNote.CallRingingStatus,
                    "in-progress" => CommunicationNote.ConnectedStatus,
                    "completed" => CommunicationNote.CompletedStatus,
                    "no-answer" => CommunicationNote.UnansweredStatus,
                    "queued" => CommunicationNote.QueuedStatus,
                    "failed" => CommunicationNote.FailedStatus,
                    // "initiated" =>
                    // "busy" => 
                    // "canceled" =>
                    // "missed" => 
                    // "answered" => 
                    // "forwarded" =>
                    // "abandoned" =>  
                    
                    _ => raw.Status,
                })
                .Set(x => x.Meta, new Dictionary<string, object>
                {
                    { "PhoneNumber", phoneNumber },
                    { "openphone|ConversationId", raw.ConversationId },
                    { "openphone|PhoneNumberId", raw.PhoneNumberId },
                    { "openphone|UserId", raw.UserId },
                })
            // .Set(x => x.ContentType, "text/plain")
            // .Set(x => x.Content, call.DispositionName)
            ;

        if (raw.AnsweredAt.HasValue) update.Set(x => x.Milestones[CommunicationNote.ConnectedStatus], raw.AnsweredAt.Value);
        if (raw.CompletedAt.HasValue) update.Set(x => x.Milestones[CommunicationNote.CompletedStatus], raw.CompletedAt.Value);

        if (config != null)
        {
            if (config.FlowId.HasValue) update.SetOnInsert(x => x.FlowId, config.FlowId);
            if (config.ObjectStatusId.HasValue) update.SetOnInsert(x => x.FlowId, config.ObjectStatusId);
        }

        if (direction == CommunicationDirection.Outbound)
        {
            update
                .Set(x => x.Name, $"Called {raw.To} from {raw.From}")
                // .Set(x => x.Description, $"Convertros called {call.PhoneNumber}")
                ;
        }
        else
        {
            update
                .Set(x => x.Name, $"Received Call from {raw.From} on {raw.To}")
                // .Set(x => x.Description, $"Convertros received call from {call.PhoneNumber}")
                ;
        }

        var media = raw.Media?.Select(x => new LinkedContent
        {
            ContentType = x.Type,
            Uri = x.Url,
        }).ToArray();

        if (media?.Length > 0)
        {
            // TODO: for now assumes only one and that it is a recording
            // ...
            update.Set(x => x.Attachments["Recording"], media[0]);
        }

        if (raw.Voicemail != null)
        {
            update.Set(x => x.Attachments["Voicemail"], new LinkedContent
            {
                ContentType = raw.Voicemail.Type,
                Uri = raw.Voicemail.Url,
            });
        }

        // TODO: check voicemail
        // ...

        var result = await update.UpdateAndGetOneAsync(true);
        
        if (result.Id != id)
        {
            // modified
            // ...
            _logger.LogInformation("{CommunicationNote} updated", result.Id);
            
            // TODO: fire event
            // ...
        }
        else
        {
            // created (first time)
            // ...
            _logger.LogInformation("{CommunicationNoteId} Added", result.Id);
            
            // TODO: fire event
            // ...
        }

        _logger.LogInformation("Call upserted: {ExternalId}: {Id}", externalObjectId, result.Id);
    }
}