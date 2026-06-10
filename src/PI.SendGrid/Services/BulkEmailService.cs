using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
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
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Email;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;

namespace Services;

public class BulkEmailService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly SendGridEmailService _emailService;
    private readonly ObjectTypeService _objectTypeService;

    public BulkEmailService(
        ILogger<BulkEmailService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        SendGridEmailService emailService,
        ObjectTypeService objectTypeService
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _emailService = emailService;
        _objectTypeService = objectTypeService;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.SendgridBulkEmail));
        mapper.Register<SimpleActionMessage<SendGridBulkEmailActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case SimpleActionMessage<SendGridBulkEmailActionOptions> action:
                    evt.Acknowledge();
                    await ProcessAsync(action);
                    break;

                default:
                    Logger.LogError("Unexpected {Body}", evt.Body.GetType().FullName);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message");
        }
        finally
        {
            evt.Acknowledge();
        }
    }

    private async Task ProcessAsync(SimpleActionMessage<SendGridBulkEmailActionOptions> action)
    {
        using var scope = Logger.AddScope(new
        {
            action.Event.ObjectType,
            action.Event.TargetId,
        });

        Logger.LogInformation("Convert Snapshot into BulkEmail");

        var result = await ConvertAsync(action);

        if (result.IsSuccess)
        {
            Logger.LogInformation("Success");

            var evt = new GenericFlowEvent(action.Event)
            {
                Description = result.Status ?? "Snapshot converted into BulkEmail",
                Action = nameof(ActionIds.SendgridBulkEmail),
                EventTypeId = action.Options.NextEventId,
            };

            await MessageBroker.DispatchAsync(evt, !result.IsSuccess);
            return;
        }

        Logger.LogError("Failed to convert: {Status}", result.Status);
        
        if (!action.Options.ErrorEventId.HasValue)
        {
            var errorEvent = new GenericFlowEvent(action.Event)
            {
                Description = result.Status ?? "Snapshot converted into BulkEmail",
                Action = nameof(ActionIds.SendgridBulkEmail),
                EventTypeId = action.Options.NextEventId,
            };
            
            await MessageBroker.DispatchAsync(errorEvent, true);
        }
        else
        {
            var errorEvent = new GenericFlowEvent(action.Event)
            {
                Description = result.Status ?? "Snapshot converted into BulkEmail",
                Action = nameof(ActionIds.SendgridBulkEmail),
                EventTypeId = action.Options.ErrorEventId,
            };
            
            await MessageBroker.DispatchAsync(errorEvent);
        }
    }

    private async Task<Result<BulkEmail>> ConvertAsync(SimpleActionMessage<SendGridBulkEmailActionOptions> action)
    {
        var accountId = action.Event.AccountId;
        var entityContext = new AccountContext(accountId);

        var (data, auth) = await _emailService.GetIntegrationSettingsAsync(entityContext);
        if (string.IsNullOrEmpty(auth?.APIKey))
        {
            return Result<BulkEmail>.Error("Missing Integration data");
        }

        var snapshot = await _connection.Filter<BulkEmail>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.TargetId)
            .FirstOrDefaultAsync();

        if (snapshot == null)
        {
            Logger.LogError("Snapshot not found");
            return Result.Error<BulkEmail>("Snapshot not found");
        }

        var unlayerTemplateId = snapshot.UnlayerTemplateId;

        var updateQuery = _connection.Filter<BulkEmail>()
                .Eq(x => x.AccountId, action.Event.AccountId)
                .Eq(x => x.Id, action.Event.TargetId)
                // .Eq(x => x.ObjectType, nameof(Snapshot))
                .Ne(x => x.End, null)
                .Eq(x => x.GenerationFlowRunId, null)
                .Eq(x => x.Error, null)
                .Update
                .Set(x => x.GenerationFlowRunId, action.Event.RunId)
                .Set(x => x.GenerationStartedOn, DateTime.UtcNow)
            ;

        // TODO: allow event to always overwrite?
        // instead of only when the current is still a snapshot?
        // ... 
        if (snapshot.ObjectType == nameof(Snapshot))
        {
            // validate event information 
            // event has to include missing settings 
            var meta = action.Event.Meta
                .DistinctBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.Value);

            var strFields = new[]
            {
                nameof(BulkEmail.FromName),
                nameof(BulkEmail.FromEmail),
                nameof(BulkEmail.ToNameField),
                nameof(BulkEmail.ToEmailField),
                nameof(BulkEmail.Subject),
                nameof(BulkEmail.UnlayerTemplateId),
            };

            var input = new Dictionary<string, string>();
            foreach (var field in strFields)
            {
                if (!meta.TryGetStrParam(field, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    Logger.LogError("Missing required {Field}", field);
                    return Result.Error<BulkEmail>($"Missing required field: {field}");
                }

                input.Add(field, value);
            }

            if (!Guid.TryParse(input[nameof(BulkEmail.UnlayerTemplateId)], out var templateId))
            {
                Logger.LogError("Invalid {UnlayerTemplateId}", input[nameof(BulkEmail.UnlayerTemplateId)]);
                return Result.Error<BulkEmail>($"Invalid Template");
            }

            unlayerTemplateId ??= templateId;

            // validate fields before starting
            var fields = snapshot.DataView.Fields.ToDictionary(x => x.Name);

            if (!fields.TryGetValue(input[nameof(BulkEmail.ToNameField)], out var toNameField))
            {
                Logger.LogError("{ToNameField} not found", input[nameof(BulkEmail.ToNameField)]);
                return Result.Error<BulkEmail>("ToNameField not found");
            }

            if (!fields.TryGetValue(input[nameof(BulkEmail.ToEmailField)], out var toEmailField))
            {
                Logger.LogError("{ToNameField} not found", input[nameof(BulkEmail.ToNameField)]);
                return Result.Error<BulkEmail>("ToNameField not found");
            }

            updateQuery
                .Set(x => x.ObjectType, nameof(BulkEmail))
                .Set(x => x.FromName, input[nameof(BulkEmail.FromName)])
                .Set(x => x.FromEmail, input[nameof(BulkEmail.FromEmail)])
                .Set(x => x.ToNameField, input[nameof(BulkEmail.ToNameField)])
                .Set(x => x.ToEmailField, input[nameof(BulkEmail.ToEmailField)])
                .Set(x => x.Subject, input[nameof(BulkEmail.Subject)])
                .Set(x => x.UnlayerTemplateId, templateId)
                ;
        }

        var template = await _connection.Filter<UnlayerTemplate>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, unlayerTemplateId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(template?.Html))
        {
            Logger.LogError("Couldn't find template {UnlayerTemplateId}", unlayerTemplateId);
            return Result<BulkEmail>.Error("Couldn't find Unlayer Template");
        }

        var user = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, snapshot.CreatedById)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            Logger.LogError("Couldn't find  {CreatedById}", snapshot.CreatedById);
            return Result<BulkEmail>.Error("Couldn't find Creator");
        }

        // TODO: VALIDATE we have all the fields we may need
        // ...

        var context = user.Context;

        // try to determine the timezone
        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, snapshot.EntityId)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(entity?.TimeZoneId)) updateQuery.Set(x => x.TimeZoneId, entity.TimeZoneId);

        // convert snapshot into bulkEmail and flag start of generation
        var bulkEmail = await updateQuery
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        if (bulkEmail == null)
        {
            Logger.LogError("Snapshot in invalid state");
            return Result.Error<BulkEmail>("Snapshot is in invalid state");
        }

        bulkEmail = await GenerateAsync(context, bulkEmail, template, action, data);

        return Result.Success(bulkEmail);
    }

    private async Task<BulkEmail> GenerateAsync(IEntityContext context, BulkEmail bulkEmail, UnlayerTemplate template,
        SimpleActionMessage<SendGridBulkEmailActionOptions> action, SendGridIntegration.Data data)
    {
        var sendGridObjectType = await _objectTypeService.GetAsync(context, nameof(SendGridEmailMessage));

        var subjectResolver = bulkEmail.Subject.Contains("{{") ? Handlebars.Compile(bulkEmail.Subject) : null;
        var htmlBodyResolver = Handlebars.Compile(template.Html);
        var plainBodyResolver = string.IsNullOrEmpty(template.Plain) ? null : Handlebars.Compile(template.Plain);
        var unsubscribeTemplateResolver = string.IsNullOrEmpty(data.UnsubscribeUrlTemplate)
            ? null
            : Handlebars.Compile(data.UnsubscribeUrlTemplate);

        var cursor = _connection.Filter<ExpandoObject>(bulkEmail.CollectionName)
            .Eq(nameof(SnapshotData.AccountId), bulkEmail.AccountId)
            .Eq(nameof(SnapshotData.SnapshotId), bulkEmail.Id)
            .SortAsc(Model.IdFieldName)
            .ToCursor();

        var invalidEmailCount = 0;
        var createdCount = 0;
        var errorCount = 0;

        var emails = new HashSet<string>();
        while (await cursor.MoveNextAsync())
        {
            foreach (var obj in cursor.Current)
            {
                try
                {
                    var emailResult = await generateEmailAsync(obj);
                    if (emailResult.IsError)
                    {
                        errorCount++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to generate email");
                    errorCount++;
                }
            }
        }

        var start = default(DateTime?); //  bulkEmail.ScheduledStart;
        if (!start.HasValue || start.Value < DateTime.UtcNow) start = DateTime.UtcNow.AddMinutes(5);

        var updated = await _connection.Filter<BulkEmail>()
            .Eq(x => x.AccountId, bulkEmail.AccountId)
            .Eq(x => x.Id, bulkEmail.Id)
            .Update
            .Set(x => x.GeneratedCount, createdCount)
            .Set(x => x.InvalidEmailCount, invalidEmailCount)
            .Set(x => x.GenerateFailuresCount, errorCount)
            .Set(x => x.GenerationEndedOn, DateTime.UtcNow)
            .Set(x => x.DoNotQueueBefore, start)
            // .Set(x => x.FlowId, action.Options.FlowId)
            // .Set(x => x.ObjectStatusId, action.Options.ObjectStatusId)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        // if (bulkEmail.ObjectStatusId != action.Options.ObjectStatusId)
        // {
        //     // object status has changed
        //     var evt = new GenericFlowEvent(action.Event)
        //     {
        //         Description = "Snapshot converted into Email",
        //         Action = nameof(ActionIds.SendgridBulkEmail),
        //     };
        //
        //     await MessageBroker.DispatchAsync(evt, EventIds.OnStatusEntered);
        // }

        return updated;

        async Task<Result<SendGridEmailMessage>> generateEmailAsync(ExpandoObject record)
        {
            if (record is not IDictionary<string, object> dict)
            {
                return Result.Error<SendGridEmailMessage>("Invalid object type");
            }

            if (!record.TryGetFieldValue(bulkEmail.ToNameField, out var toNameObj) || toNameObj is not string toName)
            {
                toName = null;
            }

            if (!record.TryGetFieldValue(bulkEmail.ToEmailField, out var toEmailObj) ||
                toEmailObj is not string toEmail || string.IsNullOrWhiteSpace(toEmail))
            {
                invalidEmailCount++;
                return Result.Error<SendGridEmailMessage>("Missing to email");
            }

            toEmail = Lead.GetNormalizedEmail(toEmail);

            if (emails.Contains(toEmail))
            {
                Logger.LogInformation("{Email} is a duplicate", toEmail);

                invalidEmailCount++;
                return Result.Error<SendGridEmailMessage>("Email address already included in this bulk email send");
            }

            emails.Add(toEmail);

            var futureEmailId = Model.NewObjectId();

            if (unsubscribeTemplateResolver != null)
            {
                dict["Action"] = new
                {
                    EmailId = futureEmailId,
                };

                var unsubscribeUrl = unsubscribeTemplateResolver.Invoke(record);
                dict["Action"] = new
                {
                    EmailId = futureEmailId,
                    UnsubscribeUrl = unsubscribeUrl,
                    UnsubscribeLink = $"<a href=\"{unsubscribeUrl}\" target=\"_blank\">Unsubscribe</a>",
                };
            }

            var emailMessage = new EmailMessage
            {
                From = new EmailAddress
                {
                    Name = bulkEmail.FromName,
                    Email = bulkEmail.FromEmail,
                },
                To = new[]
                {
                    new EmailAddress
                    {
                        Name = toName,
                        Email = toEmail,
                    }
                },
                Subject = subjectResolver != null ? subjectResolver.Invoke(record) : bulkEmail.Subject,
                HtmlBody = htmlBodyResolver.Invoke(record),
                PlainBody = plainBodyResolver?.Invoke(record),
            };

            // if (!string.IsNullOrEmpty(action.Options.BCC))
            // {
            //     var bcc = ResolveContent(context, action.Options.BCC);
            //
            //     emailMessage.BCC = bcc.Split(",")
            //         .Where(x => x != null)
            //         .Select(x => new EmailAddress
            //         {
            //             Email = x.Trim(),
            //         })
            //         .ToArray();
            // }

            return await createSendGridEmailMessageAsync();

            async Task<Result<SendGridEmailMessage>> createSendGridEmailMessageAsync()
            {
                var sgem = new SendGridEmailMessage
                {
                    Id = futureEmailId,
                    CreatedOn = DateTime.UtcNow,
                    AccountId = context.AccountId.Value,
                    EntityId = context.AccountId.Value, // ???
                    FlowRunId = bulkEmail.GenerationFlowRunId.Value,
                    TriggerObjectType = bulkEmail.ObjectType,
                    TriggerObjectId = bulkEmail.Id,
                    FlowId = sendGridObjectType?.InitialFlowId,
                    ObjectStatusId = sendGridObjectType?.InitialObjectStatusId,
                    Message = emailMessage,
                    Refs = new List<KeyValuePair<string, object>>
                    {
                        new($"{bulkEmail.ObjectType}Id", bulkEmail.Id),
                    }
                };

                // add id 
                if (record.TryResolvePathGuidValue($"{Model.IdFieldName}", out var snapshotDataId))
                {
                    sgem.Refs.Add(new KeyValuePair<string, object>($"{nameof(SnapshotData)}Id", snapshotDataId));
                }

                if (record.TryResolvePathGuidValue($"Properties|{Model.IdFieldName}", out var sourceObjectId))
                {
                    sgem.Refs.Add(new KeyValuePair<string, object>($"{bulkEmail.SourceObjectType}Id", sourceObjectId));
                }

                // add original references
                foreach (var refs in bulkEmail.DataView.Fields.OfType<ReferenceField>())
                {
                    if (record.TryResolvePathGuidValue(refs.Name, out var value))
                    {
                        sgem.Refs.Add(new KeyValuePair<string, object>($"{refs.ReferenceFieldOptions.ObjectType}Id",
                            value));
                    }
                }

                await _connection.InsertAsync(sgem);
                createdCount++;
                // await _objectTypeService.FireCreateEventAsync(context, record);

                return Result.Success(sgem);
            }
        }
    }

    /// <summary>
    /// Queue all emails pending
    /// </summary>
    public async Task<JobResult> QueueAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var count = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await QueueNextAsync(context);
            if (result == null) break;

            count++;
        }

        return new JobResult
        {
            Message = count > 0 ? $"Sent {count} emails" : "nothing to do",
            Result = new Dictionary<string, object>
            {
                { "Total", count },
                { "Processed", count },
            },
        };
    }

    public async Task<BulkEmail> QueueNextAsync(IEntityContext context)
    {
        var now = DateTime.UtcNow;

        var next = await _connection.Filter<Snapshot, BulkEmail>()
            .Eq(x => x.AccountId, context.AccountId)
            .Ne(x => x.GenerationEndedOn, null)
            .Eq(x => x.QueueFinishedOn, null)
            .Lt(x => x.DoNotQueueBefore, now)
            .Ne(x => x.IsActive, false)
            .SortDesc(x => x.Priority)
            .SortAsc(x => x.DoNotQueueBefore)
            .Update
            .Set(x => x.DoNotQueueBefore, now)
            .Set(x => x.LastModifiedOn, now)
            .UpdateAndGetOneAsync();

        if (next == null)
        {
            Logger.LogInformation("Nothing to send");
            return null;
        }

        var result = await QueueNextAsync(context, next);

        // TODO: fire update event?
        // ...

        return result;
    }

    private async Task<BulkEmail> QueueNextAsync(IEntityContext context, BulkEmail bulkEmail)
    {
        var now = DateTime.UtcNow;

        using var scope = Logger.AddScope(new
        {
            BulkEmailId = bulkEmail.Id,
            bulkEmail.TimeZoneId,
        });

        if (!string.IsNullOrEmpty(bulkEmail.TimeZoneId))
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(bulkEmail.TimeZoneId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(now, tz);
            if (local.Hour < 8 || local.Hour > 20)
            {
                if (local.Hour > 20) local = local.AddDays(1);
                var utc = TimeZoneInfo.ConvertTimeToUtc(new DateTime(local.Year, local.Month, local.Day, 8, 0, 0), tz);

                Logger.LogInformation("{LocalTime} out of business hours, reset to {DoNotQueueBefore}", local, utc);

                return await _connection.Filter<Snapshot, BulkEmail>()
                    .Eq(x => x.AccountId, bulkEmail.AccountId)
                    .Eq(x => x.Id, bulkEmail.Id)
                    .Update
                    .Set(x => x.DoNotQueueBefore, utc)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .UpdateAndGetOneAsync();
            }
        }

        Logger.LogInformation("Process email");

        var next = await _connection.Filter<SendGridEmailMessage>()
            .Eq(x => x.AccountId, bulkEmail.AccountId)
            .Eq(x => x.TriggerObjectType, nameof(BulkEmail))
            .Eq(x => x.TriggerObjectId, bulkEmail.Id)
            .Eq(x => x.Queued, null)
            .SortAsc(x => x.Id)
            .Update
            .Set(x => x.Queued, now)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        if (next == null)
        {
            Logger.LogInformation("No emails left to send");

            bulkEmail = await _connection.Filter<Snapshot, BulkEmail>()
                .Eq(x => x.AccountId, bulkEmail.AccountId)
                .Eq(x => x.Id, bulkEmail.Id)
                .Update
                .Set(x => x.DoNotQueueBefore, null)
                .Set(x => x.QueueFinishedOn, now)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateAndGetOneAsync();

            var evt = new GenericFlowEvent(bulkEmail)
            {
                Description = "Finished sending e-mails",
                EventTypeId = EventIds.OnBulkEmailDone,
            };

            evt.TryAddMetaValue(nameof(BulkEmail.QueueFinishedOn), now);
            evt.TryAddMetaValue(nameof(BulkEmail.Subject), bulkEmail.Subject);
            evt.TryAddMetaValue(nameof(BulkEmail.QueuedCount), bulkEmail.QueuedCount);
            evt.TryAddMetaValue(nameof(BulkEmail.QueuedFailuresCount), bulkEmail.QueuedFailuresCount);

            await MessageBroker.DispatchAsync(evt);

            return bulkEmail;
        }

        return await QueueAsync(context, next);
    }

    private async Task<BulkEmail> QueueAsync(IEntityContext context, SendGridEmailMessage email)
    {
        var to = email.Message.To.FirstOrDefault();
        using var scope = Logger.AddScope(new
        {
            SendGridEmailMessageId = email.Id,
            to?.Email,
            to?.Name,
        });

        Logger.LogInformation("Send email");

        var result = await _emailService.SendAsync(context, email);
        // var result = Result.Error<SendGridEmailMessage>("not sending yet");

        var query = _connection.Filter<Snapshot, BulkEmail>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, email.TriggerObjectId)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow);

        if (result.IsSuccess)
        {
            Logger.LogInformation("Email sent successfully");
            query.Inc(x => x.QueuedCount, 1);
        }
        else
        {
            Logger.LogInformation("Failed to send email: {Status}", result.Status);

            query.Inc(x => x.QueuedFailuresCount, 1);

            // update email 
            email = await _connection.Filter<SendGridEmailMessage>()
                .Eq(x => x.AccountId, email.AccountId)
                .Eq(x => x.Id, email.Id)
                .Update
                .Set(x => x.Error, result.Status)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateAndGetOneAsync();
        }

        // TODO: fire update event for email ?
        // ...

        return await query.UpdateAndGetOneAsync();
    }
}