using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

public class SendSMSActionBuilder : AbstractFlowActionBuilder<SendSMSActionOptions, SendSMSAction.Message>
{
    public override string Name => "Send SMS";
    public override Guid Id => ActionIds.SendSMS;
    public override string IconName => IntegrationIds.Twilio.ToString();

    public override string[] InputObjectTypes => null;
    // new[]
    // {
    //     nameof(Lead),
    //     nameof(Appointment),
    //     nameof(User),
    //     // ...
    // };

    public SendSMSActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new SendSMSAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, SendSMSActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return flowActionContext.ObjectType switch
            {
                nameof(Lead) or nameof(Appointment) => new SelectField
                {
                    Name = nameof(SendSMSActionOptions.To).ToCamelCase(),
                    Label = "To",
                    SelectFieldOptions = new SelectFieldOptionsBuilder
                    {
                        { nameof(SendSMSActionOptions.Tos.Custom), "Custom" },
                        { nameof(SendSMSActionOptions.Tos.Contact), "Contact" },
                    },
                    DefaultValue = opts?.To ?? SendSMSActionOptions.Tos.Contact,
                    IsRequired = true,
                },
                _ => new HiddenField
                {
                    Name = nameof(SendSMSActionOptions.To).ToCamelCase(),
                    DefaultValue = opts?.To ?? SendSMSActionOptions.Tos.Custom,
                },
            };

            yield return new TextField
            {
                Name = nameof(SendSMSActionOptions.Entity).ToCamelCase(),
                Label = "Entity",
                IsRequired = false,
                Visible = [$"{nameof(SendSMSActionOptions.To).ToCamelCase()}=='{nameof(SendSMSActionOptions.Tos.Custom)}'"],
                DefaultValue = opts?.Entity,
            };

            yield return new ExpressionField
            {
                Name = nameof(SendSMSActionOptions.PhoneNumber).ToCamelCase(),
                Label = "Phone Number",
                IsRequired = false,
                Visible = [$"{nameof(SendSMSActionOptions.To).ToCamelCase()}=='{nameof(SendSMSActionOptions.Tos.Custom)}'"],
                DefaultValue = opts?.PhoneNumber,
                ExpressionFieldOptions = new ExpressionFieldOptions
                {
                    ValueField = new TextField
                    {
                    }
                }
            };

            yield return new TextField
            {
                Name = nameof(SendSMSActionOptions.Message).ToCamelCase(),
                Label = "Message (template)",
                IsRequired = true,
                DefaultValue = opts?.Message,
                TextFieldOptions =
                {
                    Multline = true,
                    AllowExpressions = true,
                },
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, SendSMSActionOptions options)
    {
        step.Description = "Send SMS using Twilio";
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Send SMS", "SMS sent", NextEventName);
        var (evt2, output2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Failed to Send SMS", "Failed to send SMS`", ErrorEventName);
        options.NextEventId = evt.Id;
        options.ErrorEventId = evt2.Id;
        options.Output =
        [
            output,
            output2
        ];
    }
}