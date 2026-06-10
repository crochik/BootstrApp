using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
// using Elastic.Apm.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.IDP.API.Client;
using PI.Shared.Constants;
using PI.Shared.Data.Models;
using PI.Shared.Data.Mongo.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;
using Twilio.AspNet.Common;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using Twilio.TwiML;
using User = PI.Shared.Models.User;

namespace Services;

public class SMSService
{
    public const bool SendAsMMS = true;
    public const string TwilioProvider = "Twilio";
    
    private string CallbackUrlTemplate => "https://" + _host + "/twilio/v1/SMS({{id}})/Status";

    private readonly ILogger<SMSService> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly string _host;

    public SMSService(
        ILogger<SMSService> logger,
        IConfiguration configuration,
        MongoConnection connection,
        ObjectTypeService objectTypeService
    )
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;

        _host = configuration.GetSection(nameof(SMSService)).GetValue<string>("Host");
        _logger.LogInformation("Using {Host}", _host);
    }

    public async Task<Result<CommunicationNote>> SendAsync(IEntityContext context, string toPhoneNumber, string message, IEnumerable<KeyValuePair<string, object>> refs)
    {
        var list = await EntityIntegrationAdapter.GetTrunkByIdAsync(_connection, context.EntityId.Value, IntegrationIds.Twilio);
        var config = list.MinBy(x => x.Level);
        if (config == null) throw new ForbiddenException(context);
        var data = config?.GetData<TwilioIntegration.Data>();
        if (string.IsNullOrEmpty(toPhoneNumber))
        {
            throw new NotFoundException("No phone number for recipient");
        }

        var objectType = await _objectTypeService.GetAsync(context, CommunicationNote.OutboundSMS_ObjectType);

        var note = new CommunicationNote
        {
            Id = Guid.NewGuid(),
            AccountId = context.AccountId.Value,
            EntityId = context.EntityId.Value,
            FlowId = objectType?.InitialFlowId,
            ObjectStatusId = objectType?.InitialObjectStatusId,
            Content = message,
            // ContentFormat = ContentFormat.PlainText,
            ContentType = "text/plain",
            CreatedOn = DateTime.UtcNow,
            Direction = CommunicationDirection.Outbound,
            CommunicationChannel = CommunicationChannel.SMS,
            Parties = getParties().ToArray(),
            Refs = new List<KeyValuePair<string, object>>(refs)
            {
                new($"{nameof(Integration)}Id", IntegrationIds.Twilio),
            }
        };

        await _objectTypeService.InsertAsync(context, note, e =>
        {
            e.Description ??= "SMS message queued";
            e.Action ??= "ObjectCreated";
            e.RefValues ??= new List<KeyValuePair<string, object>>();
            e.RefValues.AddRange(note.Refs);
        });

        var statusCallbackUrl = CallbackUrlTemplate.Replace("{{id}}", note.Id.ToString());

        return await SendAsync(config, note, new Uri(statusCallbackUrl));

        IEnumerable<CommunicationParty> getParties()
        {
            yield return new CommunicationParty
            {
                Direction = CommunicationDirection.Outbound,
                Address = toPhoneNumber,
            };

            if (!string.IsNullOrWhiteSpace(data.PhoneNumber))
            {
                yield return new CommunicationParty
                {
                    Direction = CommunicationDirection.Inbound,
                    Address = data.PhoneNumber,
                };
            }
        }
    }

    private static string GetStatus(MessageResource.StatusEnum statusEnum)
    {
        var status = statusEnum.ToString() switch
        {
            "queued" => "Queued",
            "sending" => "Sending",
            "sent" => "Sent",
            "failed" => "Failed",
            "delivered" => "Delivered",
            "undelivered" => "Undelivered",
            "receiving" => "Receiving",
            "received" => "Received",
            "accepted" => "Accepted",
            "scheduled" => "Scheduled",
            "read" => "Read",
            "partially_delivered" => "Partially Delivered",
            "canceled" => "Canceled",
            _ => null
        };

        return status;
    }

    private async Task<Result<CommunicationNote>> SendAsync(IIntegration config, CommunicationNote message, Uri statusCallbackUrl)
    {
        var data = config?.GetData<TwilioIntegration.Data>();
        var auth = config?.GetAuthentication<TwilioIntegration.Authentication>();
        var client = new TwilioRestClient(data.AccountSid, auth.Token);

        var sender = message.Parties.FirstOrDefault(x => x.Direction == CommunicationDirection.Inbound);
        var receiver = message.Parties.FirstOrDefault(x => x.Direction == CommunicationDirection.Outbound);

        try
        {
            var msg = await MessageResource.CreateAsync(
                body: message.Content,
                to: new Twilio.Types.PhoneNumber(receiver.Address),
                client: client,

                // either from or messaging service id should be null
                from: !string.IsNullOrEmpty(sender?.Address) ? new Twilio.Types.PhoneNumber(sender.Address) : null,
                messagingServiceSid: data.MessagingServiceSid,
                statusCallback: statusCallbackUrl,
                sendAsMms: SendAsMMS
                // mediaUrl: 

                // applicationSid
                // provideFeedback
            );

            _logger.LogInformation("Message queued: {Status} {ErrorCode} {ErrorMessage}", msg.Status, msg.ErrorCode, msg.ErrorMessage);

            var update = _connection.Filter<CommunicationNote>()
                .Eq(x => x.AccountId, message.AccountId)
                .Eq(x => x.Id, message.Id)
                .Update
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.Provider, TwilioProvider)
                .Set(x => x.ExternalId, msg.Sid);

            if (msg.Status.Equals(MessageResource.StatusEnum.Failed) || msg.Status.Equals(MessageResource.StatusEnum.Undelivered) || msg.Status.Equals(MessageResource.StatusEnum.Canceled))
            {
                var status = $"{GetStatus(msg.Status)}: {msg.ErrorMessage} ({msg.ErrorCode})";
                await update
                    .Set(x => x.Status, status)
                    .Set(x => x.IsActive, false)
                    .UpdateAndGetOneAsync();

                return Result.Error<CommunicationNote>(status);
            }

            var result = await update
                .Set(x => x.Status, GetStatus(msg.Status))
                .UpdateAndGetOneAsync();

            return Result.Success(result);
        }
        catch (ApiException e)
        {
            _logger.LogError(e, "Failed to send message");

            await _connection.Filter<CommunicationNote>()
                .Eq(x => x.AccountId, message.AccountId)
                .Eq(x => x.Id, message.Id)
                .Update
                .Set(x => x.IsActive, false)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.Status, e.Message)
                .UpdateOneAsync();

            throw;
        }
    }

    public async Task OnStatusChangeAsync(Guid id, SmsStatusCallbackRequest request)
    {
        var optOut = false;
        var status = SMSService.GetStatus(request.MessageStatus);
        if (request.MessageStatus == "failed")
        {
            if (request.ErrorCode == "21610")
            {
                optOut = true;
                status = "Phone number has opted out";
            }
            else
            {
                status = $"Failed #{request.ErrorCode}";
            }
        }

        var isFinal = request.MessageStatus switch
        {
            "failed" => true,
            "delivered" => true,
            "undelivered" => true,
            "partially_delivered" => true,
            "canceled" => true,
            _ => false
        };

        var now = DateTime.UtcNow;
        var update = _connection.Filter<CommunicationNote>()
                .Eq(x => x.Id, id)
                .Update
                .Set(x => x.Status, status)
                .Set(x => x.Milestones[status], now)
                .Set(x => x.LastModifiedOn, now)
                .Set(x => x.LastActor, Actor.Current)
            ;

        var modifiedFields = new Dictionary<string, object>
        {
            { nameof(CommunicationNote.Status), status },
            { $"{nameof(CommunicationNote.Milestones)}|{status}", now },
        };

        if (isFinal && request.MessageStatus != "delivered")
        {
            update.Set(x => x.IsActive, false);

            modifiedFields.Add(nameof(CommunicationNote.IsActive), false);
        }

        var message = await update.UpdateAndGetOneAsync();

        if (message.Direction == CommunicationDirection.Outbound && message.Parties.All(x => x.Direction != CommunicationDirection.Inbound) && !string.IsNullOrWhiteSpace(request.From))
        {
            // didn't know the from number at the time it was sent
            await _connection.Filter<CommunicationNote>()
                .Eq(x => x.Id, id)
                .NotBuilder(q => q.ElemMatchBuilder(x => x.Parties, q => q.Eq(x => x.Direction, CommunicationDirection.Inbound)))
                .Update
                .Set(x => x.Status, status)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, Actor.Current)
                .Push(x => x.Parties, new CommunicationParty
                {
                    Direction = CommunicationDirection.Inbound,
                    Address = request.From,
                })
                .UpdateOneAsync();

            modifiedFields.Add(nameof(CommunicationNote.Parties), "[...]");
        }

        if (message.FlowId.HasValue && isFinal)
        {
            await _objectTypeService.FireObjectUpdatedAsync(
                new AccountContext(message.AccountId),
                message,
                modifiedFields,
                e =>
                {
                    e.Description = $"Message {request.MessageStatus}";
                    e.SetMetaValue(nameof(SmsStatusCallbackRequest.MessageStatus), request.MessageStatus);
                    e.RefValues ??= new List<KeyValuePair<string, object>>();
                    e.RefValues.AddRange(message.Refs);
                }
            );
        }

        if (optOut && message.Direction == CommunicationDirection.Outbound)
        {
            // async error from a send
            await OptOutLeadsAsync(message);    
        }
    }

    private async Task OptOutLeadsAsync(CommunicationNote message)
    {
        var leadId = message.Refs?.FirstOrDefault(x => x.Key == "LeadId");
        if (leadId.HasValue)
        {
            _logger.LogInformation("{LeadId} has asked to STOP", leadId);
        }
   
        var phoneNumber = message.Parties.FirstOrDefault(x => x.Direction == CommunicationDirection.Outbound);
        if (phoneNumber?.Address == null)
        {
            _logger.LogError("Failed to get phone number while trying to opt out");
            return;
        }
        
        _logger.LogInformation("Opting out recent leads for {NormalizedPhoneNumber}", phoneNumber.Address);

        var leads = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, message.AccountId)
            .Eq(x => x.NormalizedPhoneNumber, phoneNumber.Address)
            .Ne(x => x.IsActive, false)
            .Ne(x => x.CommunicationPreferences[CommunicationChannel.SMS], CommunicationPreference.OptedOut)
            .Gt(x => x.LastModifiedOn, DateTime.UtcNow.AddDays(-60))
            .FindAsync();

        foreach (var lead in leads)
        {
            var result = await _connection.Filter<Lead>()
                .Eq(x => x.Id, lead.Id)
                .Ne(x => x.CommunicationPreferences[CommunicationChannel.SMS], CommunicationPreference.OptedOut)
                .Update
                .Set(x => x.CommunicationPreferences[CommunicationChannel.SMS], CommunicationPreference.OptedOut)
                .UpdateOneAsync();

            if (result.ModifiedCount == 1)
            {
                _logger.LogInformation("Opting out {LeadId} from SMS", lead.Id);

                await _objectTypeService.FireObjectUpdatedAsync(
                    new AccountContext(message.AccountId),
                    lead,
                    new Dictionary<string, object>
                    {
                        { $"{nameof(Lead.CommunicationPreferences)}|{nameof(CommunicationChannel.SMS)}", CommunicationPreference.OptedOut }  
                    },
                    e =>
                    {
                        e.Description = $"{phoneNumber.Address} asked Twilio to STOP";
                        e.SetMetaValue(nameof(Lead.NormalizedPhoneNumber), phoneNumber.Address);
                        e.RefValues ??= new List<KeyValuePair<string, object>>();
                        e.AddRefValue(nameof(CommunicationNote), message.Id);
                    }
                );
            }
        }
    }

    public async Task<MessagingResponse> OnReceivedAsync(Guid entityId, SmsRequest request)
    {
        using var scope = _logger.AddScope(new
        {
            EntityId = entityId,
            FromNumber = request.From,
            ToNumber = request.To,
            request.OptOutType
        });

        _logger.LogInformation("Received SMS message");

        if (!PhoneNumber.TryParse(request.From, out var from))
        {
            _logger.LogError("Failed to parse from number");
            return null;
        }

        // entity for phone number
        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.Id, entityId)
            .FirstOrDefaultAsync();

        var context = entity.Context;
        // TODO: confirm that this entity is associated with the To from number?
        // ...

        // find most recent lead with phone number
        var query = _connection.Filter<Lead>()
                .Eq(x => x.AccountId, entity.AccountId)
                .Eq(x => x.NormalizedPhoneNumber, from.International)
                .Ne(x => x.IsActive, false)
            ;

        switch (entity)
        {
            case Account:
                break;

            case Organization org:
                query.Eq(x => x.EntityId, org.Id);
                break;

            case User user:
                if (user.UserRoleId != nameof(EntityRoleId.Admin))
                {
                    query.Eq(x => x.EntityId, user.OrganizationId.Value);
                }

                break;

            default:
                _logger.LogError("Unexpected entity");
                return null;
        }

        query.SortDesc(x => x.CreatedOn);
        var lead = await query.FirstOrDefaultAsync();
        if (lead == null)
        {
            _logger.LogInformation("Did not find any lead for this phone number");
            
            // TODO: create lead? 
            // ...
        }
        else
        {
            _logger.LogInformation("Received message from {LeadId}", lead.Id);    
        }

        var objectType = await _objectTypeService.GetAsync(context, CommunicationNote.InboundSMS_ObjectType);

        var note = new CommunicationNote
        {
            Id = Guid.NewGuid(),
            AccountId = entity.AccountId,
            EntityId = lead?.EntityId ?? entity.Id, 
            FlowId = objectType?.InitialFlowId,
            ObjectStatusId = objectType?.InitialObjectStatusId,
            Content = request.Body,
            // ContentFormat = ContentFormat.PlainText,
            ContentType = "text/plain",
            CreatedOn = DateTime.UtcNow,
            Direction = CommunicationDirection.Inbound,
            Provider = TwilioProvider,
            CommunicationChannel = CommunicationChannel.SMS,
            Parties = new[]
            {
                new CommunicationParty
                {
                    Direction = CommunicationDirection.Inbound,
                    Address = request.To,
                },
                new CommunicationParty
                {
                    Direction = CommunicationDirection.Outbound,
                    Address = request.From,
                },
            },
            Refs = new List<KeyValuePair<string, object>>
            {
                new($"{nameof(Integration)}Id", IntegrationIds.Twilio),
            }
        };

        if (lead != null)
        {
            note.Refs.Add(new KeyValuePair<string, object>($"{nameof(Lead)}Id", lead.Id));
        }

        note = await _objectTypeService.InsertAsync(context, note, e =>
        {
            e.Description ??= "SMS message received";
            e.Action ??= "ObjectCreated";
            foreach (var kvp in note.Refs)
            {
                e.AddRefValue(kvp.Key, kvp.Value);
            }
        });

        if (lead?.FlowId.HasValue ?? false)
        {
            // TODO: should this be just handled by the flow in the InboundSMS object?
            // ...
            await _objectTypeService.FireEventAsync(context, lead, EventIds.OnSMSReceived, null, e =>
            {
                e.Description ??= "SMS message received";
                e.Action ??= "SMSReceived";

                e.SetMetaValue("Message", note.Content);
                e.AddRefValue(note);
            });
        }

        if (request.OptOutType == "STOP")
        {
            await OptOutLeadsAsync(note);
        }

        return null;
    }
}