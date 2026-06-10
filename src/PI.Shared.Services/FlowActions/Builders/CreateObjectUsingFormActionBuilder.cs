using System;
using System.Collections.Generic;
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

public class CreateObjectUsingFormActionBuilder(ILogger<CreateObjectUsingFormActionBuilder> logger, MongoConnection connection, ObjectTypeService objectTypeService)
    : AbstractFlowActionBuilder<CreateObjectUsingFormActionOptions, SimpleActionMessage<CreateObjectUsingFormActionOptions>>(connection)
{
    public override Guid Id => ActionIds.CreateObjectUsingForm;
    public override string Name => nameof(ActionIds.CreateObjectUsingForm);
    public override string Description => "Create Object (v2)";
    public override string[] InputObjectTypes => null;

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        if (options is not CreateObjectUsingFormActionOptions opts)
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

    protected override async ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, CreateObjectUsingFormActionOptions opts = null)
    {
        var objectType = opts == null ? null : await objectTypeService.GetAsync(flowActionContext.EntityContext, opts.ObjectType);

        var fields = new List<FormField>
        {
            new ReferenceField
            {
                Name = nameof(CreateObjectUsingFormActionOptions.ObjectType),
                Label = "Object Type",
                IsRequired = true,
                DefaultValue = objectType?.FullName,
                Enable = objectType != null ? new[] { "false" } : null,
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
            }
        };

        if (objectType != null)
        {
            var accountContext = new AccountContext(flowActionContext.EntityContext.AccountId.Value);
            var formOptions = new ActionBuilderGetFormOptions(accountContext);
            
            var objectField = new ObjectField
            {
                Name = nameof(CreateObjectUsingFormActionOptions.Object),
                Label = "Object",
                IsRequired = true,
                DefaultValue = opts.Object,
                ObjectFieldOptions = new ObjectFieldOptions
                {
                    ObjectType = opts.ObjectType,
                }
            };

            // LoadObjectFieldAsync expects the object to NOT be flattened
            var inflated = ObjectTypeService.UnflattenFirstLevel(opts.Object);
            await objectTypeService.LoadObjectFieldAsync(accountContext, FormName.Edit, objectField, inflated, formOptions);
                
            fields.Add(objectField);
        }
        else
        {
            fields.Add(
                new ObjectField
                {
                    Name = nameof(CreateObjectUsingFormActionOptions.Object),
                    Label = "Object",
                    IsRequired = true,
                    ObjectFieldOptions = new ObjectFieldOptions
                    {
                        ObjectType = "Object", 
                        AddFormUrls = new Dictionary<string, string>
                        {
                            { "Request", "/api/v1/FlowActionBuilder/{{ObjectType}}/Add" }
                        },
                    },
                    Visible = [nameof(CreateObjectUsingFormActionOptions.ObjectType)],
                }
            );
        }
        
        fields.Add(
            new TextField
            {
                Name = nameof(OpenApiOperationActionOptions.Alias),
                Label = "Object Alias",
                DefaultValue = opts?.Alias
            }
        );

        return fields.ToArray();
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, CreateObjectUsingFormActionOptions options)
    {
        step.Description = $"Create {options.ObjectType}";
        
        var (evt1, output1) = await AddEventAsync(
            context,
            flow.ObjectType,
            step.CurrentStatusId,
            CreateObjectUsingFormActionOptions.ObjectCreatedEvent,
            "Object Created",
            CreateObjectUsingFormActionOptions.ObjectCreatedEvent
        );
        
        var (evt2, output2) = await AddEventAsync(
            context,
            flow.ObjectType,
            step.CurrentStatusId,
            CreateObjectUsingFormActionOptions.FailToCreateObjectEvent,
            "Error Creating Object",
            CreateObjectUsingFormActionOptions.FailToCreateObjectEvent
        );

        options.Output =
        [
            output1,
            output2
        ];
    }
}