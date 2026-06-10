using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PI.Shared.Constants;
using PI.Shared.Data.Models;
using PI.Shared.Data.Mongo.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using SendGrid;
using SendGrid.Helpers.Mail;
using StrongGrid;
using StrongGrid.Models.Webhooks;

namespace Services;

public class SendGridEmailService
{
    private readonly ILogger<SendGridEmailService> _logger;
    private readonly MongoConnection _connection;

    /// <summary>
    /// Replace all "Field Paths" (using "|") to "_" 
    /// </summary>
    /// <param name="obj"></param>
    public static void DuplicatePropertiesUsingUnderscore(object obj)
    {
        if (obj == null) return;
        if (obj is IDictionary<string, JToken> jDict)
        {
            var keys = jDict.Keys.ToArray();
            foreach (var key in keys)
            {
                var value = jDict[key];
                DuplicatePropertiesUsingUnderscore(value);

                if (key.Contains('|'))
                {
                    // jDict.Remove(key);
                    jDict.TryAdd(key.Replace('|', '_'), value);
                }
            }
        }

        if (obj is IDictionary<string, object> dict)
        {
            var keys = dict.Keys.ToArray();
            foreach (var key in keys)
            {
                var value = dict[key];
                DuplicatePropertiesUsingUnderscore(value);

                if (key.Contains('|'))
                {
                    // dict.Remove(key);
                    dict.TryAdd(key.Replace('|', '_'), value);
                }
            }
        }
    }

    public SendGridEmailService(
        ILogger<SendGridEmailService> logger,
        MongoConnection connection
    )
    {
        _logger = logger;
        _connection = connection;
    }

    public async Task<(SendGridIntegration.Data Data, SendGridIntegration.Authentication Auth)> GetIntegrationSettingsAsync(IEntityContext context)
    {
        var list = await EntityIntegrationAdapter.GetTrunkByIdAsync(_connection, context.EntityId.Value, IntegrationIds.SendGrid);
        var settings = list.MinBy(x => x.Level);
        var data = settings?.GetData<SendGridIntegration.Data>();
        var auth = settings?.GetAuthentication<SendGridIntegration.Authentication>();

        return (data, auth);
    }

    /// <summary>
    /// Bypass creating message and unsubscribe check to send test email 
    /// </summary>
    public async Task<bool> SendEmailAsync(IEntityContext context, EmailMessage emailMessage)
    {
        var (data, auth) = await GetIntegrationSettingsAsync(context);
        if (string.IsNullOrEmpty(data?.FromEmail) || string.IsNullOrEmpty(data?.FromName) || string.IsNullOrEmpty(auth?.APIKey))
        {
            throw new ForbiddenException("Integration configuration not found");
        }

        var msg = Map(emailMessage);
        return await SendAsync(auth.APIKey, msg);
    }

    private static SendGridMessage Map(SendGridEmailMessage emailMessage)
    {
        var msg = Map(emailMessage.Message);

        msg.AddCustomArgs(new Dictionary<string, string>
        {
            { nameof(SendGridEmailMessage.AccountId), emailMessage.AccountId.ToString() },
            { nameof(SendGridEmailMessage.FlowRunId), emailMessage.FlowRunId.ToString() },
            { nameof(SendGridEmailMessage.Id), emailMessage.Id.ToString() },
        });

        return msg;
    }

    private static SendGridMessage Map(EmailMessage email)
    {
        var msg = new SendGridMessage()
        {
            From = new SendGrid.Helpers.Mail.EmailAddress
            {
                Name = email.From.Name,
                Email = email.From.Email
            },
            Subject = email.Subject,
            PlainTextContent = email.PlainBody,
            HtmlContent = email.HtmlBody,
        };

        var To = email.To.ToList().ConvertAll(e => new SendGrid.Helpers.Mail.EmailAddress
        {
            Name = e.Name,
            Email = e.Email
        });
        msg.AddTos(To);

        if (email.CC != null)
        {
            foreach (var recipient in email.CC.Where(x=>!string.IsNullOrWhiteSpace(x.Email)))
            {
                msg.AddCc(recipient.Email, recipient.Name);
            }
        }

        if (email.BCC != null)
        {
            foreach (var recipient in email.BCC.Where(x=>!string.IsNullOrWhiteSpace(x.Email)))
            {
                msg.AddBcc(recipient.Email, recipient.Name);
            }
        }

        if (!string.IsNullOrWhiteSpace(email.ReplyTo?.Email))
        {
            msg.AddReplyTo(email.ReplyTo.Email, email.ReplyTo.Name);
        }

        if (email.TemplateId != null)
        {
            msg.TemplateId = email.TemplateId;

            if (email.TemplateData != null)
            {
                msg.SetTemplateData(email.TemplateData);
            }
        }

        if (email.Contents?.Length > 0)
        {
            var index = 0;
            foreach (var content in email.Contents)
            {
                if (content is PI.Shared.Models.Attachment attachment)
                {
                    msg.AddAttachment(new SendGrid.Helpers.Mail.Attachment
                    {
                        Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(attachment.Content)),
                        ContentId = attachment.ContentId ?? $"attachment{++index}",
                        Type = attachment.ContentType,
                        Disposition = attachment.Inline.GetValueOrDefault() ? "inline" : null,
                        Filename = attachment.Filename ?? $"attachment{++index}",
                    });
                    continue;
                }

                msg.AddContent(content.ContentType, content.Content);
            }
        }

        return msg;
    }

    private async Task<Result<SendGridMessage>> SendAsync(string apiKey, SendGridMessage msg)
    {
        var client = new SendGridClient(apiKey);

        var response = await client.SendEmailAsync(msg);

        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
            case HttpStatusCode.Created:
            case HttpStatusCode.Accepted:
                break;

            default:
                var message = await response.Body.ReadAsStringAsync();
                return Result<SendGridMessage>.Error($"Failed to send email with code {response.StatusCode}: {message}");
        }

        return Result.Success(msg);
    }

    /// <summary>
    /// Send email previously generated
    /// - will make a last minute check for unsubscribed  
    /// </summary>
    public async Task<Result<SendGridMessage>> SendAsync(IEntityContext context, SendGridEmailMessage msg)
    {
        var (_, auth) = await GetIntegrationSettingsAsync(context);
        return await SendAsync(auth.APIKey, msg);
    }

    public async Task<Result<SendGridMessage>> SendAsync(string apiKey, SendGridEmailMessage emailMessage)
    {
        var anyUnsubscribed = await _connection.Filter<SendGridEmailUnsubscribe>()
            .In(x => x.Email, emailMessage.Message.To.Select(x => x.Email.ToLowerInvariant()))
            .FirstOrDefaultAsync();

        if (anyUnsubscribed != null)
        {
            return Result.Error<SendGridMessage>($"{anyUnsubscribed.Email} has unsubscribed");
        }

        var msg = Map(emailMessage);
        return await SendAsync(apiKey, msg);
    }

    public async Task ParseEventsAsync(IEntityContext context, string requestBody, string signature, string timestamp)
    {
        var (data, auth) = await GetIntegrationSettingsAsync(context);
        if (string.IsNullOrEmpty(data?.WebhookSignatureKey)) throw new ForbiddenException("Missing signature key");

        var parser = new WebhookParser();
        var events = parser.ParseSignedEventsWebhook(requestBody, data.WebhookSignatureKey, signature, timestamp);
        foreach (var evt in events)
        {
            await ProcessAsync(context, evt);
        }
    }

    private async Task ProcessAsync(IEntityContext context, Event evt)
    {
        var emailAddress = evt.Email?.ToLowerInvariant();
        var timestamp = evt.Timestamp;

        if (!evt.UniqueArguments.TryGetValue(nameof(SendGridEmailMessage.AccountId), out var accountIdStr))
        {
            _logger.LogError("Missing AccountId");
        }

        if (!evt.UniqueArguments.TryGetValue(nameof(SendGridEmailMessage.FlowRunId), out var flowRunId))
        {
            _logger.LogError("Missing FlowRunId");
        }

        if (!evt.UniqueArguments.TryGetValue(nameof(SendGridEmailMessage.Id), out var sendGridEmailMessageId))
        {
            _logger.LogError("Missing Id");
        }

        using var scope = _logger.AddScope(new
        {
            evt.EventType,
            evt.Email,
            evt.Timestamp,
            evt.MessageId,
            AccountId = accountIdStr,
            FlowRunId = flowRunId,
            SendGridEmailMessageId = sendGridEmailMessageId,
        });

        _logger.LogInformation("Received Event");

        if (!Guid.TryParse(accountIdStr, out var accountId))
        {
            accountId = Guid.Empty;
        }

        if (!Guid.TryParse(sendGridEmailMessageId, out var id))
        {
            id = Guid.Empty;
        }

        await _connection.Filter<SendGridEmailEvent>()
            .Eq(x => x.ExternalId, evt.InternalEventId)
            .Update
            .SetOnInsert(x => x.AccountId, accountId)
            .SetOnInsert(x => x.ExternalId, evt.InternalEventId)
            .SetOnInsert(x => x.Id, Guid.NewGuid())
            .SetOnInsert(x => x.Event, evt)
            .SetOnInsert(x => x.SendGridEmailMessageId, id)
            .SetOnInsert(x => x.Email, emailAddress)
            .UpdateOneAsync(true);

        var query = _connection.Filter<SendGridEmailMessage>()
            .Eq(x => x.AccountId, accountId)
            .Eq(x => x.Id, id);

        var email = evt switch
        {
            DroppedEvent droppedEvent => await query
                .Eq(x => x.Dropped, null)
                .ElemMatchBuilder(x => x.Message.To, q => q.Eq(x => x.Email, emailAddress))
                .Update
                .Set(x => x.Dropped, timestamp)
                .Set(x => x.DroppedReason, droppedEvent.Reason)
                .Set(x => x.Error, $"Delivery failed: {droppedEvent.Reason}")
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
                .UpdateAndGetOneAsync(),

            OpenedEvent openedEvent => await query
                .Eq(x => x.Opened, null)
                .ElemMatchBuilder(x => x.Message.To, q => q.Eq(x => x.Email, emailAddress))
                .Update
                .Set(x => x.Opened, timestamp)
                .Set(x => x.OpenedByMachine, openedEvent.MachineOpen)
                .Set(x => x.OpenedIpAddress, openedEvent.IpAddress)
                .Set(x => x.OpenedContentType, openedEvent.ContentType)
                .Set(x => x.OpenedByUserAgent, openedEvent.UserAgent)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
                .UpdateAndGetOneAsync(),

            DeliveredEvent deliveredEvent => await query
                .Eq(x => x.Delivered, null)
                .ElemMatchBuilder(x => x.Message.To, q => q.Eq(x => x.Email, emailAddress))
                .Update
                .Set(x => x.Delivered, timestamp)
                .Set(x => x.DeliveredResponse, deliveredEvent.Response)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
                .UpdateAndGetOneAsync(),

            SpamReportEvent spamReportEvent => await query
                .Eq(x => x.SpamReported, null)
                .Update
                .Set(x => x.SpamReported, timestamp)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
                .UpdateAndGetOneAsync(),

            BouncedEvent bouncedEvent => await query
                .Eq(x => x.Bounced, null)
                .Update
                .Set(x => x.Bounced, timestamp)
                .Set(x => x.BounceType, bouncedEvent.Type.ToString())
                .Set(x => x.BounceReason, bouncedEvent.Reason)
                .Set(x => x.BounceStatus, bouncedEvent.Status)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
                .UpdateAndGetOneAsync(),

            _ => null,

            // case UnsubscribeEvent unsubscribeEvent:
            //     break;
        };

        if (email == null)
        {
            _logger.LogInformation("Email had already been updated (or does not exist)");
            return;
        }

        _logger.LogInformation("Updated Email");

        // TODO: fire event 
        // ...

        if (email.TriggerObjectType != nameof(BulkEmail))
        {
            return;
        }

        var bulkQuery = _connection.Filter<BulkEmail>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, email.TriggerObjectId)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow);

        bulkQuery = evt switch
        {
            DroppedEvent => bulkQuery.Inc(x => x.DroppedCount, 1),
            OpenedEvent => bulkQuery.Inc(x => x.OpenedCount, 1),
            DeliveredEvent => bulkQuery.Inc(x => x.DeliveredCount, 1),
            SpamReportEvent => bulkQuery.Inc(x => x.SpamReportCount, 1),
            BouncedEvent => bulkQuery.Inc(x => x.BouncedCount, 1),
            _ => null,
        };

        if (bulkQuery == null) return;

        var bulkEmail = await bulkQuery.UpdateAndGetOneAsync();
        if (bulkEmail == null) return;

        // TODO: fire event 
        // ...
    }
}