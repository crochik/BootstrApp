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

public class PostToSlackActionBuilder : AbstractFlowActionBuilder<PostToSlackActionOptions, PostToSlackAction.Message>
{
    public override string Name => "Post to Slack Channel";

    public override Guid Id => ActionIds.PostToSlackChannel;
    public override string IconName => IntegrationIds.Slack.ToString();

    public override string[] InputObjectTypes => null;

    public PostToSlackActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new PostToSlackAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, PostToSlackActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new SelectField
            {
                Name = nameof(PostToSlackActionOptions.To).ToCamelCase(),
                Label = "To",
                SelectFieldOptions = new SelectFieldOptionsBuilder
                {
                    { nameof(PostToSlackActionOptions.Tos.Account), "Account" },
                    { nameof(PostToSlackActionOptions.Tos.Custom), "Custom" },
                    { nameof(PostToSlackActionOptions.Tos.System), "System" }
                },
                DefaultValue = opts?.To ?? PostToSlackActionOptions.Tos.Account,
                IsRequired = true,
            };
            yield return new TextField
            {
                Name = nameof(PostToSlackActionOptions.Url).ToCamelCase(),
                Label = "URL",
                IsRequired = true,
                Visible = new[] { $"{nameof(PostToSlackActionOptions.To).ToCamelCase()}=='{nameof(PostToSlackActionOptions.Tos.Custom)}'" },
                DefaultValue = opts?.Url,
            };
            yield return new TextField
            {
                Name = nameof(PostToSlackActionOptions.Message).ToCamelCase(),
                Label = "Message",
                TextFieldOptions =
                {
                    Multline = true,
                },
                IsRequired = true,
                DefaultValue = opts?.Message,
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, PostToSlackActionOptions options)
    {
        step.Description = "Post Message to Slack";
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Post Message to Slack", "Message posted to Slack", NextEventName);
        options.NextEventId = evt.Id;
        options.Output = new[]
        {
            output,
        };
    }
}