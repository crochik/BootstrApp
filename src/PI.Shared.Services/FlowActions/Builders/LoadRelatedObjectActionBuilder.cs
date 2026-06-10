using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

public class LoadRelatedObjectActionBuilder : AbstractFlowActionBuilder<LoadRelatedObjectActionOptions, SimpleActionMessage<LoadRelatedObjectActionOptions>>
{
    public override string Name => "Load Related Object";

    public override Guid Id => ActionIds.LoadRelatedObject;

    // public override string IconName => ActionIds.LoadRelatedObject.ToString();

    public override string[] InputObjectTypes => null;

    public LoadRelatedObjectActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        if (options is not LoadRelatedObjectActionOptions opts)
        {
            throw new BadRequestException("Invalid options for action");
        }

        return new SimpleActionMessage<LoadRelatedObjectActionOptions>(evt, opts);
    }

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, LoadRelatedObjectActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            // obsolete (only for backwards compatibility)
            if (step != null && opts?.RelatedObject != null)
            {
                yield return new TextField
                {
                    Name = nameof(LoadRelatedObjectActionOptions.ParentObject).ToCamelCase(),
                    Label = "Parent Object",
                    Visible = new[]
                    {
                        nameof(LoadRelatedObjectActionOptions.RelatedObject).ToCamelCase()
                    },
                    DefaultValue = opts?.ParentObject,
                };

                yield return new TextField
                {
                    Name = nameof(LoadRelatedObjectActionOptions.RelatedObject).ToCamelCase(),
                    Label = "Related Object",
                    Visible = new[]
                    {
                        nameof(LoadRelatedObjectActionOptions.RelatedObject).ToCamelCase()
                    },
                    DefaultValue = opts?.RelatedObject,
                };
            }

            yield return new TextField
            {
                Name = nameof(LoadRelatedObjectActionOptions.RelatedObjects).ToCamelCase(),
                Label = "Related Objects: {{Parent}}.{{RelatedObject}}",
                Visible = new[]
                {
                    $"!{nameof(LoadRelatedObjectActionOptions.RelatedObject).ToCamelCase()}"
                },
                TextFieldOptions = new TextFieldOptions
                {
                    Multline = true,
                },
                DefaultValue = opts?.RelatedObjects,
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, LoadRelatedObjectActionOptions options)
    {
        step.Description = "Load Related Object(s)";
        var (evt1, out1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Load Related Object(s)", "Loaded related object(s)", NextEventName);
        var (evt2, out2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Related Object not found", "Related object not found", "notfound");
        options.NextEventId = evt1.Id;
        options.NotFoundEventId = evt2.Id;
        options.Output = new[]
        {
            out1,
            out2,
        };
    }

    public override async ValueTask<IEnumerable<Placeholder>> GetPlaceholdersForOutputAsync(IEntityContext context, Flow flow, ActionOptions triggerOptions, IEnumerable<Placeholder> placeholders, Guid stepEventIdTrigger)
    {
        var list = await base.GetPlaceholdersForOutputAsync(context, flow, triggerOptions, placeholders, stepEventIdTrigger);

        if (triggerOptions is LoadRelatedObjectActionOptions opts && opts.NextEventId == stepEventIdTrigger)
        {
            // TODO: load object type so it can infer the object types that will be generated
            // ...

            list = list.Concat(opts.GetTargetLoadedObjects(flow.ObjectType)
                .Select(x => new Placeholder
                    {
                        Name = "{{Objects." + x + "}}",
                        Type = Placeholder.PlaceholderType.Object,
                        ObjectType = "*", // TODO:...
                        Description = "Object of type ....", // TODO: ...
                    }
                )
            );
        }

        return list;
    }
}