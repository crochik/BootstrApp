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

public class PostToGoogleActionBuilder : AbstractFlowActionBuilder<PostToGoogleChatActionOptions, PostToGoogleChatAction.Message>
{
    public override string Name => "Post to Google Channel";

    public override Guid Id => ActionIds.PostToGoogleChat;
    public override string IconName => IntegrationIds.Google.ToString();

    public override string[] InputObjectTypes => null;

    public PostToGoogleActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new PostToGoogleChatAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, PostToGoogleChatActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new SelectField
            {
                Name = nameof(PostToGoogleChatActionOptions.To).ToCamelCase(),
                Label = "To",
                SelectFieldOptions = new SelectFieldOptionsBuilder
                {
                    { nameof(PostToGoogleChatActionOptions.Tos.Account), "Account" },
                    { nameof(PostToGoogleChatActionOptions.Tos.Custom), "Custom" },
                    { nameof(PostToGoogleChatActionOptions.Tos.System), "System" }
                },
                DefaultValue = opts?.To ?? PostToGoogleChatActionOptions.Tos.Account,
                IsRequired = true,
            };
            yield return new TextField
            {
                Name = nameof(PostToGoogleChatActionOptions.Url).ToCamelCase(),
                Label = "URL",
                IsRequired = true,
                Visible = new[] { $"{nameof(PostToGoogleChatActionOptions.To).ToCamelCase()}=='{nameof(PostToGoogleChatActionOptions.Tos.Custom)}'" },
                DefaultValue = opts?.Url,
            };
            yield return new TextField
            {
                Name = nameof(PostToGoogleChatActionOptions.Message).ToCamelCase(),
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

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, PostToGoogleChatActionOptions options)
    {
        step.Description = "Post Message to Google Chat";
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Post Message to Google Chat", "Message posted to Google Chat", NextEventName);
        options.NextEventId = evt.Id;
        options.Output = new[]
        {
            output,
        };
    }
}