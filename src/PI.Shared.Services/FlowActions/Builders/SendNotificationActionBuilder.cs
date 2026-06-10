using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Notifications;

namespace FlowActions;

public class SendNotificationActionBuilder : AbstractFlowActionBuilder<SendNotificationActionOptions, SimpleActionMessage<SendNotificationActionOptions>>
{
    public override string Name => "Create Notification";
    public override Guid Id => ActionIds.SendNotification;
    public override string[] InputObjectTypes => null;

    public SendNotificationActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new SimpleActionMessage<SendNotificationActionOptions>(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, SendNotificationActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new TextField
            {
                Name = nameof(SendNotificationActionOptions.EntityId).ToCamelCase(),
                Label = "To (expression)",
                IsRequired = true,
                DefaultValue = opts?.EntityId,
                TextFieldOptions = new TextFieldOptions
                {
                    AllowExpressions = true,
                }
            };
            yield return new TextField
            {
                Name = nameof(SendNotificationActionOptions.Title).ToCamelCase(),
                Label = "Title (expression)",
                IsRequired = true,
                DefaultValue = opts?.Title,
                TextFieldOptions = new TextFieldOptions
                {
                    AllowExpressions = true,
                }
            };
            yield return new TextField
            {
                Name = nameof(SendNotificationActionOptions.Message).ToCamelCase(),
                Label = "Message (handlebars template)",
                IsRequired = true,
                DefaultValue = opts?.Message,
                TextFieldOptions = new TextFieldOptions
                {
                    AllowExpressions = true,
                    Multline = true,
                }
            };
            yield return new TextField
            {
                Name = nameof(SendNotificationActionOptions.Url).ToCamelCase(),
                Label = "Launch Url (expression)",
                IsRequired = false,
                DefaultValue = opts?.Url,
                TextFieldOptions = new TextFieldOptions
                {
                    AllowExpressions = true,
                }
            };
            yield return new TextField
            {
                Name = nameof(SendNotificationActionOptions.Action).ToCamelCase(),
                Label = "In-App Action (expression)",
                IsRequired = false,
                DefaultValue = opts?.Action,
                TextFieldOptions = new TextFieldOptions
                {
                    AllowExpressions = true,
                }
            };
            yield return new ReferenceField
            {
                Name = nameof(SendNotificationActionOptions.Category).ToCamelCase(),
                Label = "Category",
                IsRequired = true,
                ReferenceFieldOptions= new ReferenceFieldOptions()
                {
                    ObjectType = nameof(NotificationCategory),
                    ForeignFieldName = nameof(NotificationCategory.ExternalId),
                },
                DefaultValue = opts?.Category,
            };
            yield return new TextField
            {
                Name = nameof(SendNotificationActionOptions.ClientId).ToCamelCase(),
                Label = "Client Application (expression)",
                IsRequired = false,
                DefaultValue = opts?.ClientId,
                TextFieldOptions = new TextFieldOptions
                {
                    AllowExpressions = true,
                }
            };
            // .Add(new ReferenceField
            // {
            //     Name = nameof(SendNotificationActionOptions.FlowId).ToCamelCase(),
            //     Label = "Flow",
            //     IsRequired = false,
            //     ReferenceFieldOptions = new ReferenceFieldOptions
            //     {
            //         ObjectType = nameof(Flow),
            //         Criteria = new []
            //         {
            //             new Condition
            //             {
            //                 FieldName = nameof(ObjectType),
            //                 Value = nameof(Notification)
            //             }
            //         }
            //     }
            // })
            // .Add(new ReferenceField
            // {
            //     Name = nameof(SendNotificationActionOptions.ObjectStatusId).ToCamelCase(),
            //     Label = "Initial Status",
            //     IsRequired = false,
            //     ReferenceFieldOptions = new ReferenceFieldOptions
            //     {
            //         ObjectType = nameof(ObjectStatus),
            //         Criteria = new []
            //         {
            //             new Condition
            //             {
            //                 FieldName = nameof(ObjectType),
            //                 Value = nameof(Notification)
            //             }
            //         }
            //     }
            // })
            // ttl        
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, SendNotificationActionOptions options)
    {
        step.Description = "Create user notification";
        var (evt1, output1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Push Notification", "Push Notification sent", "pushnotification");
        var (evt2, output2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Additional SMS Notification", "Additional SMS Notification requested", "sms");
        var (evt3, output3) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Additional Email Notification", "Additional Email Notification requested", "email");
        var (evt4, output4) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "No Subscriptions", "No Subscriptions", "nosubscriptions");

        options.PushNotificationEventId = evt1.Id;
        options.SMSNotificationEventId = evt2.Id;
        options.EmailNotificationEventId = evt3.Id;
        options.NoSubscriptionsEventId = evt4.Id;
        options.Output = new[]
        {
            output1,
            output2,
            output3,
            output4,
        };
    }

    // protected override async Task<ParsedOptions> CreateStepAsync(ParseContext context, SendNotificationActionOptions options)
    // {
    //     options.PushNotificationEventId = await GetOrCreateEventAndAddToOutputAsync(context, options, PushNotificationEventName, options.PushNotificationEventId);
    //     options.EmailNotificationEventId = await GetOrCreateEventAndAddToOutputAsync(context, options, EmailNotificationEventName, options.EmailNotificationEventId);
    //     options.SMSNotificationEventId = await GetOrCreateEventAndAddToOutputAsync(context, options, SMSNotificationEventName, options.SMSNotificationEventId);
    //
    //     return await base.CreateStepAsync(context, options);
    // }
    //
    // protected override IEnumerable<ActionOutput> GetOutputs(SendNotificationActionOptions options)
    // {
    //     yield return  new ActionOutput
    //     {
    //         Name = PushNotificationEventName,
    //         EventId = options.PushNotificationEventId,
    //         Description = "Notification Pushed",
    //     };
    //     yield return  new ActionOutput
    //     {
    //         Name = SMSNotificationEventName,
    //         EventId = options.SMSNotificationEventId,
    //         Description = "Fallback to SMS",
    //     };
    //     yield return  new ActionOutput
    //     {
    //         Name = EmailNotificationEventName,
    //         EventId = options.EmailNotificationEventId,
    //         Description = "Fallback to Email",
    //     };
    // }     
}