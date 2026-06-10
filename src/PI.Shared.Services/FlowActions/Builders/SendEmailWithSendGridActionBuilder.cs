using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using MongoDB.Bson;
using Newtonsoft.Json.Linq;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Email;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Files;

namespace FlowActions;

public class SendEmailWithSendGridActionBuilder : AbstractFlowActionBuilder<SendEmailWithSendGridActionOptions, SendEmailWithSendGridAction.Message>
{
    private const string ExistingUnlayerTemplateIdField = "existingUnlayerTemplateId";
    public override Guid Id => ActionIds.SendEmailSendgrid;

    public override string Name => "Send e-mail via SendGrid";
    public override string IconName => IntegrationIds.SendGrid.ToString();

    public override string[] InputObjectTypes => null;

    public SendEmailWithSendGridActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new SendEmailWithSendGridAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, SendEmailWithSendGridActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            var recipientFieldOptions = new SelectFieldOptionsBuilder
            {
                // { nameof(SendEmailWithSendGridActionOptions.Recipient.Account), "Account" },
                { nameof(SendEmailWithSendGridActionOptions.Recipient.Custom), "Other" }
            };

            switch (flowActionContext.ObjectType)
            {
                case nameof(Appointment):
                case nameof(Lead):
                    recipientFieldOptions.Add(nameof(SendEmailWithSendGridActionOptions.Recipient.Entity), "Entity (Organization)");
                    recipientFieldOptions.Add(nameof(SendEmailWithSendGridActionOptions.Recipient.AssignedEntity), "Assigned Entity (User)");
                    recipientFieldOptions.Add(nameof(SendEmailWithSendGridActionOptions.Recipient.Lead), "Lead");
                    break;

                case nameof(Account):
                case nameof(Organization):
                case nameof(User):
                    recipientFieldOptions.Add(nameof(SendEmailWithSendGridActionOptions.Recipient.Entity), "Entity");
                    break;
            }

            yield return new SelectField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.To).ToCamelCase(),
                Label = "To",
                SelectFieldOptions = recipientFieldOptions,
                DefaultValue = opts?.To ?? SendEmailWithSendGridActionOptions.Recipient.Custom,
                IsRequired = true,
            };

            yield return new TextField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.ToName).ToCamelCase(),
                Label = "To Name",
                Visible = new[] { $"{nameof(SendEmailWithSendGridActionOptions.To).ToCamelCase()}=='{nameof(SendEmailWithSendGridActionOptions.Recipient.Custom)}'" },
                IsRequired = true,
                DefaultValue = opts?.ToName,
            };
            yield return new TextField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.ToEmail).ToCamelCase(),
                Label = "To Email",
                Visible = new[] { $"{nameof(SendEmailWithSendGridActionOptions.To).ToCamelCase()}=='{nameof(SendEmailWithSendGridActionOptions.Recipient.Custom)}'" },
                IsRequired = true,
                DefaultValue = opts?.ToEmail,
            };
            yield return new TextField
            {
                Name = "cc",
                Label = "CC Email(s)",
                IsRequired = false,
                DefaultValue = opts?.CC,
            };
            yield return new TextField
            {
                Name = "bcc",
                Label = "BCC Email(s)",
                IsRequired = false,
                DefaultValue = opts?.BCC,
            };

            var senderFieldOptions = new SelectFieldOptionsBuilder
            {
                { nameof(SendEmailWithSendGridActionOptions.Sender.Account), "Account" },
                { nameof(SendEmailWithSendGridActionOptions.Sender.Custom), "Other" },
                // {nameof(SendEmailWithSendGridActionOptions.Sender.System), "System"},
                // {nameof(SendEmailWithSendGridActionOptions.Sender.Entity), "Entity"}
            };
            yield return new SelectField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.From).ToCamelCase(),
                Label = "From",
                SelectFieldOptions = senderFieldOptions,
                DefaultValue = opts?.From ?? SendEmailWithSendGridActionOptions.Sender.Account,
                IsRequired = true,
            };

            yield return new TextField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.FromName).ToCamelCase(),
                Label = "From Name",
                Visible = new[] { $"{nameof(SendEmailWithSendGridActionOptions.From).ToCamelCase()}=='{nameof(SendEmailWithSendGridActionOptions.Sender.Custom)}'" },
                IsRequired = true,
                DefaultValue = opts?.FromName,
            };
            yield return new TextField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.FromEmail).ToCamelCase(),
                Label = "From Email",
                Visible = new[] { $"{nameof(SendEmailWithSendGridActionOptions.From).ToCamelCase()}=='{nameof(SendEmailWithSendGridActionOptions.Sender.Custom)}'" },
                IsRequired = true,
                DefaultValue = opts?.FromEmail,
            };
            yield return new TextField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.ReplyToName).ToCamelCase(),
                Label = "Reply To Name",
                IsRequired = false,
                DefaultValue = opts?.ReplyToName,
            };
            yield return new TextField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.ReplyToEmail).ToCamelCase(),
                Label = "Reply To Email",
                IsRequired = false,
                DefaultValue = opts?.ReplyToEmail,
            };
            yield return new TextField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.Subject).ToCamelCase(),
                Label = "Subject",
                DefaultValue = opts?.Subject,
            };

            var templateSourceOptions = new SelectFieldOptionsBuilder
            {
                { nameof(SendEmailWithSendGridActionOptions.TemplateSourceOptions.SendGrid), "SendGrid" },
                { nameof(SendEmailWithSendGridActionOptions.TemplateSourceOptions.Unlayer), "Unlayer" },
                { nameof(SendEmailWithSendGridActionOptions.TemplateSourceOptions.Inline), "Inline" },
            };
            yield return new SelectField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.TemplateSource).ToCamelCase(),
                Label = "Template Source",
                SelectFieldOptions = templateSourceOptions,
                DefaultValue = opts?.TemplateSource ?? SendEmailWithSendGridActionOptions.TemplateSourceOptions.Unlayer, // ).ToCamelCase(),
                IsRequired = true,
            };

            yield return new TextField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.TemplateId).ToCamelCase(),
                Label = "SendGrid Template Id",
                IsRequired = false,
                Visible = new[] { $"{nameof(SendEmailWithSendGridActionOptions.TemplateSource).ToCamelCase()}=='{nameof(SendEmailWithSendGridActionOptions.TemplateSourceOptions.SendGrid)}'" },
                DefaultValue = opts?.TemplateId,
            };
            yield return new TextField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.PlainBody).ToCamelCase(),
                Label = "Message (plain)",
                TextFieldOptions =
                {
                    Multline = true,
                },
                // Visible = new[] { $"{nameof(SendEmailWithSendGridActionOptions.TemplateSource).ToCamelCase()}=='{nameof(SendEmailWithSendGridActionOptions.TemplateSourceOptions.Inline)}'" },
                DefaultValue = opts?.PlainBody,
            };
            yield return new TextField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.HtmlBody).ToCamelCase(),
                Label = "Message (HTML)",
                TextFieldOptions =
                {
                    Multline = true,
                },
                Visible = new[] { $"{nameof(SendEmailWithSendGridActionOptions.TemplateSource).ToCamelCase()}=='{nameof(SendEmailWithSendGridActionOptions.TemplateSourceOptions.Inline)}'" },
                DefaultValue = opts?.HtmlBody,
            };
            yield return new TextField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.UnlayerTemplateId).ToCamelCase(),
                Label = "Unlayer Email Template (optional {{...}})",
                Visible = new[] { $"{nameof(SendEmailWithSendGridActionOptions.TemplateSource).ToCamelCase()}=='{nameof(SendEmailWithSendGridActionOptions.TemplateSourceOptions.Unlayer)}'" },
                DefaultValue = opts?.UnlayerTemplateId != null && !Guid.TryParse(opts.UnlayerTemplateId, out _) ? opts.UnlayerTemplateId : null,
            };
            yield return new ReferenceField
            {
                Name = ExistingUnlayerTemplateIdField,
                Label = "Unlayer Email Template",
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = "UnlayerTemplate",
                    Criteria = new[]
                    {
                        Condition.Eq(nameof(UnlayerTemplate.IsActive), true),
                        Condition.Eq(nameof(UnlayerTemplate.TemplateType), flowActionContext.ObjectType),
                        // Condition.Eq(nameof(UnlayerTemplate.TemplatedObjectId), true),
                        // Condition.Eq(nameof(UnlayerTemplate.Tags), flowActionContext.ObjectType),
                    },
                    Actions = new[]
                    {
                        new FormAction
                        {
                            Action = FormAction.Client_New,
                            Name = "New",
                            Label = "Edit",
                        },
                        // new FormAction
                        // {
                        //     Action = "SendTestEmail",
                        //     Name = "SendTestEmail",
                        //     Label = "Send Test email"
                        // }
                    }
                },
                Visible = new[]
                {
                    $"{nameof(SendEmailWithSendGridActionOptions.TemplateSource).ToCamelCase()}=='{nameof(SendEmailWithSendGridActionOptions.TemplateSourceOptions.Unlayer)}'",
                    $"!{nameof(SendEmailWithSendGridActionOptions.UnlayerTemplateId).ToCamelCase()}"
                },
                DefaultValue = opts?.UnlayerTemplateId != null && Guid.TryParse(opts.UnlayerTemplateId, out var uuid) ? uuid : null,
            };
            yield return new TextField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.Attachment).ToCamelCase(),
                Label = "Attachment (path to object)",
                DefaultValue = opts?.Attachment,
            };
            yield return new SelectField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.AttachmentObjectType).ToCamelCase(),
                Label = "Attachment Type",
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = new OrderedDictionary()
                    {
                        { nameof(Attachment), "Attachment" },
                        { nameof(RemoteFile), "Remote File" },
                    }
                },
                Visible = new[]
                {
                    $"{nameof(SendEmailWithSendGridActionOptions.Attachment).ToCamelCase()}"
                },
                DefaultValue = opts?.AttachmentObjectType,
            };

            yield return new CheckboxField
            {
                Name = nameof(SendEmailWithSendGridActionOptions.InlineAttachment).ToCamelCase(),
                Label = "Inline Attachment",
                Visible = new[] { $"{nameof(SendEmailWithSendGridActionOptions.Attachment).ToCamelCase()}" },
                DefaultValue = opts?.InlineAttachment,
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, SendEmailWithSendGridActionOptions options)
    {
        await ValidateAsync(context, requestParameters, options);

        step.Description = "Send email using Sendgrid";
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Send Email using Sendgrid", "Email sent using Sendgrid", NextEventName);
        var (evt2, output2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Failed to Send Email using Sendgrid", "Failed to send Email using Sendgrid", ErrorEventName);
        options.NextEventId = evt.Id;
        options.ErrorEventId = evt2.Id;
        options.Output = new[]
        {
            output,
            output2,
        };
    }

    protected override async ValueTask<FlowStep> UpdateStepAsync(IEntityContext context, Flow flow, FlowStep step, Dictionary<string, object> requestParameters, SendEmailWithSendGridActionOptions options)
    {
        await ValidateAsync(context, requestParameters, options);

        var result = await base.UpdateStepAsync(context, flow, step, requestParameters, options);
        
        return result;
    }

    private ValueTask ValidateAsync(IEntityContext context, Dictionary<string, object> requestParameters, SendEmailWithSendGridActionOptions options)
    {
        // ... 
        switch (options.TemplateSource)
        {
            case SendEmailWithSendGridActionOptions.TemplateSourceOptions.Inline:
                if (string.IsNullOrWhiteSpace(options.HtmlBody) && string.IsNullOrWhiteSpace(options.PlainBody))
                {
                    throw new BadRequestException("Required body when using inline template");
                }

                break;

            case SendEmailWithSendGridActionOptions.TemplateSourceOptions.Unlayer:
            {
                var existingUnlayerTemplateId = default(Guid?);
                if (requestParameters.TryGetValue("existingUnlayerTemplateId", out var param))
                {
                    if (param is JValue jValue) param = jValue.Value;

                    existingUnlayerTemplateId = param.TryToParseObjectId(out var uuid) ? uuid : null;
                }

                if (string.IsNullOrWhiteSpace(options.UnlayerTemplateId))
                {
                    if (!existingUnlayerTemplateId.HasValue) throw new BadRequestException("Required Unlayer template id");
                    options.UnlayerTemplateId = existingUnlayerTemplateId.ToString();
                }

                break;
            }

            case SendEmailWithSendGridActionOptions.TemplateSourceOptions.SendGrid:
                if (string.IsNullOrWhiteSpace(options.TemplateId))
                {
                    throw new BadRequestException("Required SendGrid Template Id");
                }

                break;
        }

        return ValueTask.CompletedTask;
    }
}