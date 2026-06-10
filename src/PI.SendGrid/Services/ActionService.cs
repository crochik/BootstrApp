using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using HandlebarsDotNet;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Data.Models;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Email;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;
using Crochik.Extensions;
using Newtonsoft.Json;
using PI.Shared.Models.Files;

namespace Services;

public class ActionService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly SendGridEmailService _emailService;
    private readonly SMSService _smsService;
    private readonly RemoteFileService _remoteFileService;

    public ActionService(
        ILogger<ActionService> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        SendGridEmailService emailService,
        SMSService smsService,
        RemoteFileService remoteFileService
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
        _emailService = emailService;
        _smsService = smsService;
        _remoteFileService = remoteFileService;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.SendEmailSendgrid));
        mapper.Register<SendEmailWithSendGridAction.Message>();

        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.SendSMS));
        mapper.Register<SendSMSAction.Message>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var parts = evt.RoutingKey.Split('.');
            switch (evt.Body)
            {
                case SendEmailWithSendGridAction.Message message:
                    await ProcessSendEmailAsync(message);
                    break;
                case SendSMSAction.Message message:
                    await ProcessSendSMSAsync(message);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }

    private async Task ProcessSendSMSAsync(SendSMSAction.Message message)
    {
        using var scope = Logger.AddScope(new
        {
            message.Event.AccountId,
            message.Event.TargetId,
            message.Options.PhoneNumber,
        });

        Logger.LogInformation("Send SMS");

        var result = await SendSMSAsync(message);
        if (result.IsUnknown)
        {
            Logger.LogInformation("Skipped, do not fire any events");
            return;
        }

        if (!result)
        {
            Logger.LogError("Failed: {Error}", result.Status);
        }
        else
        {
            Logger.LogInformation("Sent Successfully: {Status}", result.Status);
        }

        var evt = new GenericFlowEvent(message.Event)
        {
            Description = result.Status ?? "Sent successfully",
            Action = nameof(ActionIds.SendSMS),
            EventTypeId = message.Options.NextEventId,
        };

        await MessageBroker.DispatchAsync(evt, result.IsError);
    }

    private async Task ProcessSendEmailAsync(SendEmailWithSendGridAction.Message message)
    {
        using var scope = Logger.AddScope(new
        {
            message.Event.AccountId,
            message.Event.TargetId,
            message.Options.ToEmail,
            message.Options.Subject,
        });

        Logger.LogInformation("Send Email");

        var result = await SendEmailAsync(message);
        if (result.IsUnknown)
        {
            Logger.LogInformation("Skipped sending email: {Status}", result.Status);
            return;
        }

        var evt = new GenericFlowEvent(message.Event)
        {
            Description = result.Status ?? "Sent successfully",
            Action = nameof(ActionIds.SendEmailSendgrid),
            EventTypeId = message.Options.NextEventId,
        };

        if (!result)
        {
            Logger.LogError("Failed: {Error}", result.Status);
        }
        else
        {
            Logger.LogInformation("Sent {SendGridEmailMessageId} Successfully: {Status}", result.Value.Id, result.Status);
            evt.AddRefValue(result.Value);
        }

        await MessageBroker.DispatchAsync(evt, result.IsError);
    }

    private async Task<ExpandoObject> BuildHandlebarsContext(FlowEvent evt)
    {
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.Id, evt.RunId)
            .FirstOrDefaultAsync();

        return flowRun.BuildHandlebarsContext(evt);
    }

    private async Task<Result<SendGridEmailMessage>> SendEmailAsync(SendEmailWithSendGridAction.Message action)
    {
        var accountId = action.Event.AccountId;
        var entityContext = new AccountContext(accountId);

        var (data, auth) = await _emailService.GetIntegrationSettingsAsync(entityContext);
        if (string.IsNullOrEmpty(auth?.APIKey))
        {
            return Result<SendGridEmailMessage>.Error("Missing Integration data");
        }

        switch (action.Options.From)
        {
            case SendEmailWithSendGridActionOptions.Sender.Entity:
            case SendEmailWithSendGridActionOptions.Sender.System:
            case SendEmailWithSendGridActionOptions.Sender.Account:
                // TODO: differentiate ... as now only the Account is used
                // and we assume that the sendgrid integration is defined at the account level
                // ...
                action.Options.FromEmail = data.FromEmail;
                action.Options.FromName = data.FromName;
                break;

            default:
                if (string.IsNullOrEmpty(action.Options.FromEmail))
                {
                    action.Options.FromEmail = data.FromEmail;
                    action.Options.FromName = data.FromName;
                }

                break;
        }

        var futureEmailId = Model.NewObjectId();
        var emailMessage = await BuildEmailMessage(entityContext, action, data, futureEmailId);
        if (!emailMessage.IsSuccess) return emailMessage.ConvertTo<SendGridEmailMessage>();

        var record = await CreateSendGridEmailMessageAsync(entityContext, action, emailMessage.Value, futureEmailId);
        return await SendAsync(entityContext, auth, record);
    }

    string ResolveContent(ExpandoObject context, string value)
    {
        if (value != null && value.Contains("{{"))
        {
            var result = Handlebars.Compile(value).Invoke(context);
            return result;
        }

        return value;
    }

    private async Task<Result<EmailMessage>> BuildEmailMessage(IEntityContext entityContext, SendEmailWithSendGridAction.Message action, SendGridIntegration.Data data, Guid id)
    {
        var handlebarsContext = await BuildHandlebarsContext(action.Event);
        if (!string.IsNullOrWhiteSpace(data.UnsubscribeUrlTemplate) && handlebarsContext is IDictionary<string, object> dict)
        {
            dict["Action"] = new
            {
                EmailId = id,
            };

            var unsubscribeUrl = ResolveContent(handlebarsContext, data.UnsubscribeUrlTemplate);
            dict["Action"] = new
            {
                EmailId = id,
                UnsubscribeUrl = unsubscribeUrl,
                UnsubscribeLink = $"<a href=\"{unsubscribeUrl}\" target=\"_blank\">Unsubscribe</a>",
            };

            Logger.LogInformation("{UnsubscribeUrl}", unsubscribeUrl);

            handlebarsContext = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(handlebarsContext));
        }

        var to = await ResolveToAsync(entityContext, action, handlebarsContext);
        if (!to.IsSuccess) return to.ConvertTo<EmailMessage>();

        var emailMessage = new EmailMessage
        {
            From = new EmailAddress
            {
                Name = ResolveContent(handlebarsContext, action.Options.FromName),
                Email = ResolveContent(handlebarsContext, action.Options.FromEmail),
            },
            To = to.Value.ToArray(),
        };

        if (!string.IsNullOrWhiteSpace(action.Options.ReplyToEmail))
        {
            emailMessage.ReplyTo = new EmailAddress
            {
                Email = ResolveContent(handlebarsContext, action.Options.ReplyToEmail),
                Name = ResolveContent(handlebarsContext, action.Options.ReplyToName),
            };
        }

        if (!string.IsNullOrWhiteSpace(action.Options.CC))
        {
            var cc = ResolveContent(handlebarsContext, action.Options.CC);

            emailMessage.CC = cc.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new EmailAddress
                {
                    Email = x.Trim(),
                })
                .ToArray();
        }

        if (!string.IsNullOrWhiteSpace(action.Options.BCC))
        {
            var bcc = ResolveContent(handlebarsContext, action.Options.BCC);

            emailMessage.BCC = bcc.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new EmailAddress
                {
                    Email = x.Trim(),
                })
                .ToArray();
        }

        if (!string.IsNullOrWhiteSpace(action.Options.Subject))
        {
            emailMessage.Subject = ResolveContent(handlebarsContext, action.Options.Subject);
        }

        var messageBody = ResolveContent(handlebarsContext, action.Options.PlainBody);
        switch (action.Options.TemplateSource)
        {
            case SendEmailWithSendGridActionOptions.TemplateSourceOptions.Inline:
                // TODO: check if there is a template id in the entity integration?
                // ...
                emailMessage.PlainBody = messageBody;
                emailMessage.HtmlBody = ResolveContent(handlebarsContext, action.Options.HtmlBody);
                break;

            case SendEmailWithSendGridActionOptions.TemplateSourceOptions.Unlayer:
                var templateIdStr = ResolveContent(handlebarsContext, action.Options.UnlayerTemplateId);
                if (!Guid.TryParse(templateIdStr, out var templateId))
                {
                    Logger.LogError("Couldn't parse {Value} for {UnlayerTemplateId}", templateIdStr, action.Options.UnlayerTemplateId);
                    return Result<EmailMessage>.Error("Couldn't resolve Unlayer Template Id");
                }

                var template = await _connection.Filter<UnlayerTemplate>()
                    .Eq(x => x.AccountId, entityContext.AccountId.Value)
                    .Eq(x => x.Id, templateId)
                    .Ne(x => x.IsActive, false)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(template?.Html))
                {
                    Logger.LogError("Couldn't find template {UnlayerTemplateId}", templateId);
                    return Result<EmailMessage>.Error("Couldn't find Unlayer Template");
                }

                if (!string.IsNullOrEmpty(messageBody) && handlebarsContext is IDictionary<string, object> hbDict)
                {
                    // add resolved body to context as Action|Message
                    hbDict["Action|Message"] = messageBody;
                    handlebarsContext = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(handlebarsContext));
                }

                emailMessage.HtmlBody = ResolveContent(handlebarsContext, template.Html);
                emailMessage.PlainBody = ResolveContent(handlebarsContext, template.Plain ?? action.Options.PlainBody);
                break;

            case SendEmailWithSendGridActionOptions.TemplateSourceOptions.SendGrid:
                SendGridEmailService.DuplicatePropertiesUsingUnderscore(handlebarsContext);
                emailMessage.TemplateId = action.Options.TemplateId;
                emailMessage.TemplateData = handlebarsContext;
                break;

            default:
                // before template source
                if (!string.IsNullOrEmpty(action.Options.TemplateId))
                {
                    SendGridEmailService.DuplicatePropertiesUsingUnderscore(handlebarsContext);
                    emailMessage.TemplateId = action.Options.TemplateId;
                    emailMessage.TemplateData = handlebarsContext;
                }
                else
                {
                    emailMessage.PlainBody = ResolveContent(handlebarsContext, action.Options.PlainBody);
                    emailMessage.HtmlBody = ResolveContent(handlebarsContext, action.Options.HtmlBody);
                }

                break;
        }

        // attachment
        if (!string.IsNullOrEmpty(action.Options.Attachment))
        {
            var attachment = action.Options.AttachmentObjectType switch
            {
                nameof(RemoteFile) => await BuildAttachmentFromFileContentsAsync(entityContext, handlebarsContext, action),
                nameof(Attachment) => GetAttachmentFromContext(handlebarsContext, action),
                _ => GetAttachmentFromContext(handlebarsContext, action),
            };

            if (!attachment.IsSuccess)
            {
                return attachment.ConvertTo<EmailMessage>();
            }

            emailMessage.Contents = (emailMessage.Contents ?? Enumerable.Empty<MimeContent>())
                .Append(attachment.Value)
                .ToArray();
        }

        return Result.Success(emailMessage);
    }

    private async Task<Result<Attachment>> BuildAttachmentFromFileContentsAsync(IEntityContext context, ExpandoObject handlebarsContext, SendEmailWithSendGridAction.Message action)
    {
        if (!handlebarsContext.TryResolvePathGuidValue(action.Options.Attachment, out var remoteFileId))
        {
            if (!handlebarsContext.TryResolvePathValue(action.Options.Attachment, out var attachmentObject) || attachmentObject is not ExpandoObject attachment)
            {
                return Result<Attachment>.Error("Couldn't resolve attachment");
            }

            if (!attachment.TryResolvePathGuidValue(Model.IdFieldName, out remoteFileId))
            {
                return Result<Attachment>.Error("Invalid or missing attachment object");
            }
        }

        var remoteFile = await _connection.Filter<RemoteFile>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, remoteFileId)
            .FirstOrDefaultAsync();

        if (remoteFile == null) return Result<Attachment>.Error("Invalid or missing file");

        await using var stream = await _remoteFileService.GetStreamAsync(context, remoteFile);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        return Result.Success(new Attachment
        {
            Content = content,
            ContentType = remoteFile.ContentType,
            Size = content.Length,
            Inline = action.Options.InlineAttachment,
            Filename = remoteFile.Name,
        });
    }

    private Result<Attachment> GetAttachmentFromContext(ExpandoObject context, SendEmailWithSendGridAction.Message action)
    {
        if (!context.TryResolvePathValue(action.Options.Attachment, out var attachmentObject) || attachmentObject is not ExpandoObject attachment)
        {
            return Result<Attachment>.Error("Couldn't resolve attachment");
        }

        if (!attachment.TryResolvePathValue(nameof(MimeContent.Content), out var contentObj) || contentObj is not string content)
        {
            return Result<Attachment>.Error("Invalid or missing attachment content");
        }

        if (!attachment.TryResolvePathValue(nameof(MimeContent.ContentType), out var contentTypeObj) || contentTypeObj is not string contentType)
        {
            return Result<Attachment>.Error("Invalid or missing attachment content type");
        }

        if (!attachment.TryResolvePathValue(nameof(MimeContent.Filename), out var filenameObj) || filenameObj is not string filename)
        {
            return Result<Attachment>.Error("Invalid or missing attachment filename");
        }

        return Result.Success(new Attachment
        {
            Content = content,
            ContentType = contentType,
            Size = content.Length,
            Inline = action.Options.InlineAttachment,
            Filename = filename,
        });
    }

    private async Task<Result<IEnumerable<EmailAddress>>> ResolveToAsync(IEntityContext entityContext, SendEmailWithSendGridAction.Message action, ExpandoObject context)
    {
        if (action.Options.To == SendEmailWithSendGridActionOptions.Recipient.Lead)
        {
            var lead = await GetParentLeadIfAnyAsync(action.Event);
            if (lead == null) return Result.Error<IEnumerable<EmailAddress>>("No lead");
            if (!lead.IsActive) return Result.Unknown<IEnumerable<EmailAddress>>("Lead is inactive");
            if (lead.GetCommunicationPreference(CommunicationChannel.Email) == CommunicationPreference.OptedOut) return Result.Unknown<IEnumerable<EmailAddress>>("Lead has opted out");
            if (string.IsNullOrWhiteSpace(lead.Email)) return Result.Error<IEnumerable<EmailAddress>>("No email for lead");
            return Result.Success(
                new EmailAddress
                {
                    Email = ResolveContent(context, lead.Email),
                    Name = ResolveContent(context, lead.Name),
                }.AsEnumerable()
            );
        }

        if (action.Options.To == SendEmailWithSendGridActionOptions.Recipient.AssignedEntity)
        {
            // assigned entity, for now only a "Lead" concept
            var lead = await GetParentLeadIfAnyAsync(action.Event);
            if (lead == null) return Result.Error<IEnumerable<EmailAddress>>("No lead");
            var entityId = lead.AssignedEntityId ?? lead.EntityId;

            return await getEntityEmailAddressAsync(entityId);
        }

        if (action.Options.To == SendEmailWithSendGridActionOptions.Recipient.Entity)
        {
            // entity associated with (entityOwned) object
            if (!context.TryResolvePathGuidValue("{{Objects." + action.Event.ObjectType + ".EntityId}}", out var entityId))
            {
                return Result.Error<IEnumerable<EmailAddress>>("Invalid or missing EntityId");
            }

            return await getEntityEmailAddressAsync(entityId);
        }

        // default/custom
        var emailAddress = new EmailAddress
        {
            Email = ResolveContent(context, action.Options.ToEmail),
            Name = ResolveContent(context, action.Options.ToName),
        };

        return Result.Success(emailAddress.AsEnumerable());

        async Task<Result<IEnumerable<EmailAddress>>> getEntityEmailAddressAsync(Guid entityId)
        {
            var entity = await _connection.Filter<Entity>()
                .Eq(x => x.AccountId, action.Event.AccountId)
                .Eq(x => x.Id, entityId)
                .FirstOrDefaultAsync();

            if (entity == null) return Result.Error<IEnumerable<EmailAddress>>($"Invalid or missing Entity: {entityId}");
            if (!entity.IsActive) return Result.Error<IEnumerable<EmailAddress>>("Entity is not active");
            if (string.IsNullOrWhiteSpace(entity.Email)) return Result.Error<IEnumerable<EmailAddress>>("Entity email not found");
            return Result.Success(new EmailAddress
            {
                Email = entity.Email,
                Name = entity.Name,
            }.AsEnumerable());
        }
    }

    private async Task<SendGridEmailMessage> CreateSendGridEmailMessageAsync(IEntityContext context, SendEmailWithSendGridAction.Message action, EmailMessage emailMessage, Guid id)
    {
        var sendGridObjectType = await _objectTypeService.GetAsync(context, nameof(SendGridEmailMessage));
        var record = new SendGridEmailMessage
        {
            Id = id,
            CreatedOn = DateTime.UtcNow,
            AccountId = context.AccountId.Value,
            EntityId = context.AccountId.Value, // ???
            FlowRunId = action.Event.RunId,
            TriggerObjectType = action.Event.ObjectType,
            TriggerObjectId = action.Event.TargetId,
            FlowId = sendGridObjectType?.InitialFlowId,
            ObjectStatusId = sendGridObjectType?.InitialObjectStatusId,
            Message = emailMessage,
            Refs = new List<KeyValuePair<string, object>>
            {
                new($"{action.Event.ObjectType}Id", action.Event.TargetId),
            }
        };

        // LOAD Object in the event and add all referencefields as refvalues
        var objectType = await _objectTypeService.GetAsync(context, action.Event.ObjectType);
        var obj = await _objectTypeService.GetExpandoObjectByIdAsync(context, objectType, action.Event.TargetId);
        if (obj is IDictionary<string, object> objDict)
        {
            var flatObject = objectType.UnsafeFlatten(context, objDict);
            foreach (var refs in objectType.Fields.Values.Select(x => x.Field).OfType<ReferenceField>())
            {
                if (flatObject.TryGetValue(refs.Name, out var value) && value != null)
                {
                    record.Refs.Add(new KeyValuePair<string, object>(refs.Name, value));
                }
            }
        }

        await _connection.InsertAsync(record);
        await _objectTypeService.FireCreateEventAsync(context, record);

        return record;
    }

    private async Task<Result<SendGridEmailMessage>> SendAsync(IEntityContext context, SendGridIntegration.Authentication auth, SendGridEmailMessage record)
    {
        var now = DateTime.UtcNow;
        var update = _connection.Filter<SendGridEmailMessage>()
                .Eq(x => x.Id, record.Id)
                .Update
                .Set(x => x.LastModifiedOn, now)
            ;

        var modifiedFields = new Dictionary<string, object>();
        try
        {
            var result = await _emailService.SendAsync(auth.APIKey, record);
            if (result.IsSuccess)
            {
                update.Set(x => x.Queued, now);

                modifiedFields.Add(nameof(SendGridEmailMessage.Queued), now);
            }
            else
            {
                update.Set(x => x.Error, result.Status);

                modifiedFields.Add(nameof(SendGridEmailMessage.Error), result.Status);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send message");
            update.Set(x => x.Error, ex.Message);

            modifiedFields.Add(nameof(SendGridEmailMessage.Error), ex.Message);
        }

        record = await update.UpdateAndGetOneAsync();

        if (!string.IsNullOrEmpty(record.Error)) return Result.Error<SendGridEmailMessage>(record.Error);

        await _objectTypeService.FireObjectUpdatedAsync(context, record, modifiedFields, e => { e.Description = "Email Queued"; });

        return Result.Success(record);
    }

    private async Task<Lead> GetParentLeadIfAnyAsync(FlowEvent evt)
    {
        return evt.ObjectType switch
        {
            nameof(Lead) => await loadLeadAsync(evt.TargetId),
            nameof(Appointment) => await loadFromAppointmentAsync(evt.TargetId),
            _ => null,
        };

        Task<Lead> loadLeadAsync(Guid leadId) => _connection.Filter<Lead>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.Id, leadId)
            .FirstOrDefaultAsync();

        async Task<Lead> loadFromAppointmentAsync(Guid appointmentId)
        {
            var appointment = await _connection.Filter<Appointment>()
                .Eq(x => x.AccountId, evt.AccountId)
                .Eq(x => x.Id, appointmentId)
                .FirstOrDefaultAsync();

            return appointment == null ? null : await loadLeadAsync(appointment.LeadId);
        }
    }

    private async Task<Result<Note[]>> SendSMSAsync(SendSMSAction.Message action)
    {
        var handlebarsContext = await BuildHandlebarsContext(action.Event);

        var phoneNumber = action.Options.PhoneNumber;
        var entity = default(Entity);
        var refs = Enumerable.Empty<KeyValuePair<string, object>>();
        if (action.Options.To == SendSMSActionOptions.Tos.Contact)
        {
            // special handling for leads
            // TODO: get rid of it or may have to expand to some other object types
            // ...
            var lead = await GetParentLeadIfAnyAsync(action.Event);
            if (lead == null) return Result.Error<Note[]>("Lead not found");
            if (!lead.IsActive) return Result.Error<Note[]>("Lead is inactive");
            if (string.Equals(lead.GetCommunicationPreference(CommunicationChannel.SMS), CommunicationPreference.OptedOut))
            {
                return Result.Error<Note[]>("Lead has opted out of receiving SMS");
            }

            phoneNumber = lead.NormalizedPhoneNumber;
            entity = await _connection.Filter<Entity>()
                .Eq(x => x.AccountId, lead.AccountId)
                .Eq(x => x.Id, lead.AssignedEntityId ?? lead.EntityId)
                .FirstOrDefaultAsync();

            refs = refs.Append(new($"{nameof(Lead)}Id", lead.Id));

            Logger.LogInformation("Resolved Receiver {LeadId} {NormalizedPhoneNumber} {EntityId}", lead.Id, lead.NormalizedPhoneNumber, entity?.Id);
        }

        if (entity == null && !string.IsNullOrWhiteSpace(action.Options.Entity))
        {
            var entityId = default(Guid?);
            if (!string.IsNullOrWhiteSpace(action.Options.Entity))
            {
                if (action.Options.Entity.StartsWith("{{"))
                {
                    if (!handlebarsContext.TryResolvePathGuidValue(action.Options.Entity, out var uuid))
                    {
                        return Result.Error<Note[]>($"Can't resolve EntityId: {action.Options.Entity}");
                    }

                    entityId = uuid;
                }
                else if (Guid.TryParse(action.Options.Entity, out var uuid))
                {
                    entityId = uuid;
                }
            }

            entity = entityId.HasValue
                ? await _connection.Filter<Entity>()
                    .Eq(x => x.AccountId, action.Event.AccountId)
                    .Eq(x => x.Id, entityId)
                    .Ne(x => x.IsActive, false)
                    .FirstOrDefaultAsync()
                : null;

            if (entity == null || !entity.IsActive)
            {
                return Result.Error<Note[]>($"Can't resolve EntityId: {action.Options.Entity}");
            }
        }
        
        if (string.IsNullOrWhiteSpace(action.Options.PhoneNumber))
        {
            // no phone: resolve phone number from entity
            if (entity != null)
            {
                phoneNumber = entity.Phone;
                Logger.LogInformation("No PhoneNumber in options, use {EntityId} {PhoneNumber}", entity.Id, phoneNumber);
            }
        }
        else if (action.Options.PhoneNumber.StartsWith("{{")) // TODO: should use expression evaluator service
        {
            phoneNumber = handlebarsContext.ResolvePathValue(action.Options.PhoneNumber) switch
            {
                string str => str,
                _ => null,
            };

            Logger.LogInformation("Resolved {PhoneNumber} from {OptionsPhoneNumber}", phoneNumber, action.Options.PhoneNumber);
        }
        else
        {
            // try to use as it is
            phoneNumber = action.Options.PhoneNumber;
        }

        if (string.IsNullOrWhiteSpace(phoneNumber)) return Result.Error<Note[]>("Invalid or missing phone number");
        if (PhoneNumber.TryParse(phoneNumber, out var parsed))
        {
            phoneNumber = parsed.International;
        }
        else
        {
            return Result.Error<Note[]>($"Invalid phone number: {phoneNumber}");
        }

        if (entity != null)
        {
            refs = refs.Append(new KeyValuePair<string, object>($"{entity.ObjectType}Id", entity.Id));
        }

        var context = entity?.Context ?? new AccountContext(action.Event.AccountId);
        
        // render message
        var result = Handlebars.Compile(action.Options.Message).Invoke(handlebarsContext);

        var lines = SMSService.SendAsMMS ? new[] { result } : result.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var notes = new List<Note>();
        foreach (var msg in lines)
        {
            var note = await _smsService.SendAsync(context, phoneNumber, msg, refs);
            if (note.IsError)
            {
                return Result<Note[]>.Error(note.Status);
            }

            Logger.LogInformation("Queued Message: {NoteId}", note.Value.Id);

            notes.Add(note.Value);
        }

        return Result.Success(notes.ToArray());
    }
}