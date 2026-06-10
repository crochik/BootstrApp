using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;

namespace FlowActions;

public class FireEventActionBuilder(ILogger<CreateObjectUsingFormActionBuilder> logger, MongoConnection connection, ObjectTypeService objectTypeService)
    : AbstractFlowActionBuilder<FireEventActionOptions, SimpleActionMessage<FireEventActionOptions>>(connection)
{
    public override Guid Id => ActionIds.FireEvent;
    public override string Name => nameof(ActionIds.FireEvent);
    public override string Description => "Fire Event";
    public override string[] InputObjectTypes => null;

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        if (options is not FireEventActionOptions opts)
        {
            throw new BadRequestException("Invalid options for action");
        }

        var genericOpts = new GenericActionOptions(opts)
        {
            Output = opts.Output,
        };

        opts.Output = null;

        return new SimpleActionMessage<GenericActionOptions>(evt, genericOpts);
    }

    protected override async ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, FireEventActionOptions opts = null)
    {
        var objectType = opts == null ? null : await objectTypeService.GetAsync(flowActionContext.EntityContext, opts.ObjectType);
        var eventType = opts?.ObjectType == null || opts.EventTypeId == null
            ? null
            : await connection.Filter<EventType>()
                .Eq(x => x.AccountId, flowActionContext.EntityContext.AccountId.Value)
                .Eq(x => x.ObjectType, objectType.FullName)
                .Eq(x => x.Id, opts.EventTypeId)
                .FirstOrDefaultAsync();

        var fields = new List<FormField>
        {
            new ExpressionField
            {
                Name = nameof(FireEventActionOptions.UserId),
                Label = "User",
                IsRequired = true,
                DefaultValue = opts?.UserId,
                ExpressionFieldOptions = new ExpressionFieldOptions
                {
                    ValueField = new ReferenceField
                    {
                        ReferenceFieldOptions = new ReferenceFieldOptions
                        {
                            ObjectType = nameof(User),
                            Criteria =
                            [
                                Condition.Ne(nameof(User.IsActive), false),
                            ]
                        },
                    }, 
                }
            },            
            new ReferenceField
            {
                Name = nameof(FireEventActionOptions.ObjectType),
                Label = "Object Type",
                IsRequired = true,
                DefaultValue = objectType?.FullName,
                Enable = objectType != null ? ["false"] : null,
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = nameof(ObjectType),
                    ForeignFieldName = nameof(ObjectType.FullName),
                    Criteria =
                    [
                        Condition.Ne(nameof(ObjectType.IsAbstract), true),
                        Condition.Ne(nameof(ObjectType.IsEmbedded), true)
                    ]
                },
            },
            // event type
            new ReferenceField
            {
                Name = nameof(FireEventActionOptions.EventTypeId),
                Label = "Event Type",
                IsRequired = true,
                DefaultValue = eventType?.Id,
                Enable = eventType != null ? ["false"] : null,
                Visible = [nameof(FireEventActionOptions.ObjectType)],
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = nameof(EventType),
                    Criteria =
                    [
                        Condition.Eq(nameof(EventType.ObjectType), "{{ObjectType}}"),
                    ]
                },
            },
            new ExpressionField
            {
                Name = nameof(FireEventActionOptions.ObjectId),
                Label = "Object Id",
                DefaultValue = opts?.ObjectId,
                ExpressionFieldOptions = new ExpressionFieldOptions
                {
                    ValueField = new TextField(),
                },
                Visible = [nameof(FireEventActionOptions.EventTypeId)],
            },            
            new ExpressionField
            {
                Name = nameof(FireEventActionOptions.Description),
                Label = "Event Description",
                DefaultValue = opts?.Description,
                ExpressionFieldOptions = new ExpressionFieldOptions
                {
                    ValueField = new TextField(),
                },
                Visible = [nameof(FireEventActionOptions.ObjectId)],
            },            
            new ExpressionField
            {
                Name = nameof(FireEventActionOptions.Action),
                Label = "Action",
                DefaultValue = opts?.Action,
                ExpressionFieldOptions = new ExpressionFieldOptions
                {
                    ValueField = new TextField(),
                },
                Visible = [nameof(FireEventActionOptions.ObjectId)],
            },
        };

        if (eventType != null)
        {
            var objectField = new ObjectField
            {
                Name = nameof(FireEventActionOptions.Parameters),
                Label = "Parameters",
                IsRequired = true,
                DefaultValue = opts.Parameters,
                ObjectFieldOptions = new ObjectFieldOptions
                {
                    ObjectType = "Object", // ?? 
                    EditForm = new Form
                    {
                        Name = nameof(FireEventActionOptions.Parameters),
                        Title = "Parameters",
                        Fields = [
                            new LabelField
                            {
                                Name = "Message",
                                Label = "No parameters",
                            },
                        ],
                    }
                }
            };

            // get form for event
            if (eventType.Trigger is UserTrigger trigger && trigger.Form?.Fields?.Length > 0)
            {
                var inflated = ObjectTypeService.UnflattenFirstLevel(opts.Parameters);

                objectField.ObjectFieldOptions.EditForm.Fields = trigger.Form.Fields.Select(x => new ExpressionField
                {
                    Name = x.Name,
                    Label = x.Label,
                    ApiName = x.ApiName,
                    Description = x.Description,
                    DefaultValue = x.DefaultValue,
                    // Enable = x.Enable,
                    // Visible = x.Visible,
                    ExpressionFieldOptions = new ExpressionFieldOptions
                    {
                        ValueField = x,
                    }
                }).ToArray<FormField>();

                foreach (var field in objectField.ObjectFieldOptions.EditForm.Fields)
                {
                    if (inflated.TryGetValue(field.Name, out var value))
                    {
                        field.DefaultValue = value;
                    }
                }
            }

            fields.Add(objectField);
        }
        else
        {
            fields.Add(
                new ObjectField
                {
                    Name = nameof(FireEventActionOptions.Parameters),
                    Label = "Parameters",
                    IsRequired = true,
                    ObjectFieldOptions = new ObjectFieldOptions
                    {
                        ObjectType = "Object",
                        AddFormUrls = new Dictionary<string, string>
                        {
                            { "Parameters", "/api/v1/FlowActionBuilder/EventType({{" + nameof(FireEventActionOptions.EventTypeId) + "}})/Add" }
                        },
                    },
                    Visible = [nameof(FireEventActionOptions.ObjectType), nameof(FireEventActionOptions.EventTypeId)],
                }
            );
        }

        return fields.ToArray();
    }

    protected override Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, FireEventActionOptions options)
    {
        step.Description = $"Fire Event for {options.ObjectType}";

        options.Output =
        [
        ];

        return Task.CompletedTask;
    }
}