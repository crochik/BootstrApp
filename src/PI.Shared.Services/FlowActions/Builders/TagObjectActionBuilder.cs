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
using PI.Shared.Services;

namespace FlowActions;

public class TagObjectActionBuilder : AbstractFlowActionBuilder<TagObjectActionOptions, SimpleActionMessage<TagObjectActionOptions>>
{
    public override Guid Id => ActionIds.TagObject;
    public override string Name => "Tag Object";
    public override string[] InputObjectTypes => null;
    
    public TagObjectActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        if (options is not TagObjectActionOptions opts)
        {
            throw new BadRequestException("Invalid options for action");
        }

        return new SimpleActionMessage<TagObjectActionOptions>(evt, opts);
    }

    protected override async ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, TagObjectActionOptions opts = null)
    {
        var objectType = await ObjectTypeService.GetAsync(_connection, flowActionContext.EntityContext, flowActionContext.ObjectType);
        if (objectType == null)
        {
            throw new NotFoundException($"{flowActionContext.ObjectType} not found");
        }

        var allFields = objectType.Fields.Values.Select(x => x.Field);
        var fields = Enumerable.Empty<FormField>()
                .Concat(allFields.OfType<CheckboxField>())
                .Concat(allFields.OfType<TagsField>())
                .ToDictionary(x=>x.Name, x=>x.Label ?? x.Name)
            ;
        
        return getFields();

        IEnumerable<FormField> getFields()
        {
            yield return new TextField
            {
                Name = nameof(TagObjectActionOptions.Tag).ToCamelCase(),
                Label = "Tag",
                DefaultValue = opts?.Tag,
            };

            yield return new SelectField
            {
                Name = nameof(TagObjectActionOptions.FieldName).ToCamelCase(),
                Label = "Field",
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = fields
                },
                DefaultValue = opts?.FieldName,
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, TagObjectActionOptions options)
    {
        step.Description = $"Tag '{options.FieldName}' with '{options.Tag}' if not already tagged";
        var (evt1, output1) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"Tag '{options.FieldName}' with '{options.Tag}'", $"'{options.FieldName}' tagged with '{options.Tag}'", NextEventName);
        var (evt2, output2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"'{options.FieldName}' already Tagged with '{options.Tag}'", $"'{options.FieldName}' already tagged with '{options.Tag}'", "skip");
        options.NextEventId = evt1.Id;
        options.AlreadyTaggedEventId = evt2.Id;
        options.Output = new[]
        {
            output1,
            output2,
        };
    }
}