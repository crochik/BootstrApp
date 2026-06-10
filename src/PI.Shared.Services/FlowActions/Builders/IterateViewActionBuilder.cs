using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace FlowActions;

public class IterateViewActionBuilder : AbstractFlowActionBuilder<IterateViewActionOptions, SimpleActionMessage<IterateViewActionOptions>>
{
    private const string AppDataViewId = "appDataViewId";

    public override Guid Id => ActionIds.IterateView;
    public override string Name => "Iterate over view";
    public override string[] InputObjectTypes => null;

    public IterateViewActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        if (options is not IterateViewActionOptions opts)
        {
            throw new BadRequestException("Invalid options for action");
        }

        return new SimpleActionMessage<IterateViewActionOptions>(evt, opts);
    }

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, IterateViewActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new ReferenceField()
            {
                Name = AppDataViewId,
                Label = "View",
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = nameof(AppDataView),
                    Criteria = new[]
                    {
                        Condition.Eq(nameof(AppDataView.ObjectType), flowActionContext.ObjectType),
                        Condition.Ne(nameof(AppDataView.IsActive), false),
                    },
                },
                Visible = new[]
                {
                    $"!{nameof(IterateViewActionOptions.AppDataView).ToCamelCase()}"
                },
                DefaultValue = opts?.AppDataView != null && Guid.TryParse(opts.AppDataView, out var guid) ? guid : null,
            };

            yield return new TextField
            {
                Name = nameof(IterateViewActionOptions.AppDataView).ToCamelCase(),
                Label = "View (expression)",
                Visible = new[]
                {
                    $"!{AppDataViewId}"
                },
                DefaultValue = opts?.AppDataView != null && !Guid.TryParse(opts.AppDataView, out _) ? opts.AppDataView : null,
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, IterateViewActionOptions options)
    {
        // TODO: check if view is an id and load it if it is to check it exists and use the name
        // ...
        
        step.Description = $"For each {flow.ObjectType} in View, execute...";
        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"Execute for each {flow.ObjectType} in the view", $"{flow.ObjectType} is in the view", NextEventName);
        options.NextEventId = evt.Id;
        options.Output = new[]
        {
            output,
        };
    }
}