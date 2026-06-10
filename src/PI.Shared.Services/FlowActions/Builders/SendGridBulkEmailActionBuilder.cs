using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Layout;

namespace FlowActions;

public class SendGridBulkEmailActionBuilder : AbstractFlowActionBuilder<SendGridBulkEmailActionOptions, SimpleActionMessage<SendGridBulkEmailActionOptions>>
{
    public override Guid Id => ActionIds.SendgridBulkEmail;

    public override string Name => "Bulk e-mail using SendGrid";
    public override string IconName => IntegrationIds.SendGrid.ToString();

    public override string[] InputObjectTypes => new[]
    {
        // nameof(Snapshot),
        // $"{nameof(Snapshot)}*"
        nameof(BulkEmail)
    };

    public SendGridBulkEmailActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new SimpleActionMessage<SendGridBulkEmailActionOptions>(evt, options);

    public override async Task<Form> GetFormAsync(FlowActionContext flowActionContext, Guid? objectStatusId, FlowStep step = null)
    {
        var form = await base.GetFormAsync(flowActionContext, objectStatusId, step);

        form.Layouts = new BreakpointLayouts
        {
            Medium = layout(),
        };

        return form;

        GridFormLayout layout()
        {
            return new GridFormLayout
            {
                Rows = rows()
                    .Select(row =>
                    {
                        return new GridFormRowLayout
                        {
                            Fields = row.Select(x => new GridFormFieldLayout
                            {
                                Name = x.ToCamelCase(),
                                Width = 1,
                            }).ToArray(),
                        };
                    }).ToArray(),
            };
        }

        IEnumerable<string[]> rows()
        {
            yield return new[]
            {
                nameof(SendGridBulkEmailActionOptions.BCC),
            };

            // yield return new[]
            // {
            //     nameof(SendGridBulkEmailActionOptions.FlowId),
            //     nameof(SendGridBulkEmailActionOptions.ObjectStatusId),
            // };

            if (step == null)
            {
                yield return new[]
                {
                    ObjectStatusIdFieldName,
                };
            }
        }
    }

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, SendGridBulkEmailActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new TextField
            {
                Name = nameof(SendGridBulkEmailActionOptions.BCC).ToCamelCase(),
                Label = "BCC Email(s)",
                IsRequired = false,
                DefaultValue = opts?.BCC,
            };
            
            // yield return new ReferenceField
            // {
            //     Name = nameof(SendGridBulkEmailActionOptions.FlowId).ToCamelCase(),
            //     Label = "New Flow",
            //     IsRequired = true,
            //     ReferenceFieldOptions = new ReferenceFieldOptions
            //     {
            //         ObjectType = nameof(Flow),
            //         Criteria = new[]
            //         {
            //             Condition.Eq(nameof(Flow.ObjectType), nameof(BulkEmail)),
            //         }
            //     },
            //     DefaultValue = opts?.FlowId,
            // };          
            //
            // yield return new ReferenceField
            // {
            //     Name = nameof(SendGridBulkEmailActionOptions.ObjectStatusId).ToCamelCase(),
            //     Label = "New Object Status",
            //     IsRequired = true,
            //     ReferenceFieldOptions = new ReferenceFieldOptions
            //     {
            //         ObjectType = nameof(ObjectStatus),
            //         Criteria = new[]
            //         {
            //             Condition.Eq(nameof(ObjectStatus.ObjectType), nameof(BulkEmail)),
            //         }
            //     },
            //     DefaultValue = opts?.ObjectStatusId,
            // };                 
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, SendGridBulkEmailActionOptions options)
    {
        step.Description = "Bulk email using Sendgrid";
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "BulkEmailUsingSendgridCreated", "Emails ready to send", NextEventName);
        var (evt2, output2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Failed to Create Bulk Email using Sendgrid", "Failed to send Email using Sendgrid", ErrorEventName);
        options.NextEventId = evt.Id;
        options.ErrorEventId = evt2.Id;
        options.Output = new[]
        {
            output,
            output2,
        };
    }
}