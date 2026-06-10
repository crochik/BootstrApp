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
using PI.Shared.Models.OpenAPI;
using PI.Shared.Services;

namespace FlowActions;

public class OpenApiOperationActionBuilder : AbstractFlowActionBuilder<OpenApiOperationActionOptions, SimpleActionMessage<OpenApiOperationActionOptions>>
{
    private readonly ILogger<OpenApiOperationActionBuilder> _logger;
    private readonly ObjectTypeService _objectTypeService;

    public override Guid Id => ActionIds.OpenApiOperation;
    public override string Name => nameof(ActionIds.OpenApiOperation);
    public override string Description => "Open API: Execute Operation";
    public override string[] InputObjectTypes => null;

    public OpenApiOperationActionBuilder(ILogger<OpenApiOperationActionBuilder> logger, MongoConnection connection, ObjectTypeService objectTypeService) : base(connection)
    {
        _logger = logger;
        _objectTypeService = objectTypeService;
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
    {
        if (options is not OpenApiOperationActionOptions opts)
        {
            throw new BadRequestException("Invalid options for action");
        }

        return new SimpleActionMessage<OpenApiOperationActionOptions>(evt, opts);
    }

    protected override async ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, OpenApiOperationActionOptions opts = null)
    {
        var operation = opts == null
            ? null
            : await _connection.Filter<Operation>()
                .Eq(x => x.AccountId, flowActionContext.EntityContext.AccountId)
                .Eq(x => x.Id, opts.OperationId)
                .FirstOrDefaultAsync();

        var fields = new List<FormField>
        {
            new ReferenceField
            {
                Name = nameof(OpenApiOperationActionOptions.Namespace),
                Label = "API",
                IsRequired = true,
                DefaultValue = opts?.Namespace,
                Enable = opts != null ? new[] { "false" } : null,
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = "openapi.Document",
                    ForeignFieldName = "Namespace",
                },
            },
            new ReferenceField
            {
                Name = nameof(OpenApiOperationActionOptions.OperationId),
                Label = "Operation",
                IsRequired = true,
                DefaultValue = opts?.OperationId,
                Enable = opts != null ? new[] { "false" } : null,
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = "openapi.Operation",
                    Criteria = new[]
                    {
                        Condition.Eq(nameof(ObjectType.Namespace), "{{" + nameof(OpenApiOperationActionOptions.Namespace) + "}}"),
                    }
                },
                Visible = new[] { nameof(OpenApiOperationActionOptions.Namespace) }
            },
            new ExpressionField
            {
                Name = nameof(OpenApiOperationActionOptions.EntityId),
                Label = "Entity",
                IsRequired = false,
                DefaultValue = opts?.EntityId,
                Options = new ExpressionFieldOptions
                {
                    ValueField = new ReferenceField
                    {
                        ReferenceFieldOptions = new ReferenceFieldOptions
                        {
                            ObjectType = nameof(Entity),
                        },
                    }
                }
            }
        };

        if (operation != null)
        {
            var accountContext = new AccountContext(flowActionContext.EntityContext.AccountId.Value);
            var formOptions = new ActionBuilderGetFormOptions(accountContext);

            if (operation.Request.ParametersObjectType != null)
            {
                var parametersField = new ObjectField
                {
                    Name = nameof(OpenApiOperationActionOptions.Parameters),
                    Label = "Parameters",
                    DefaultValue = opts?.Parameters,
                    ObjectFieldOptions = new ObjectFieldOptions
                    {
                        ObjectType = operation.Request.ParametersObjectType,
                    }
                };

                await _objectTypeService.LoadObjectFieldAsync(accountContext, FormName.Edit, parametersField, opts.Parameters, formOptions);

                fields.Add(parametersField);
            }
            else if (operation.Request.Parameters != null)
            {
                // TODO: build object field and form with fields
                // ...
                throw new Exception("Not implemented yet");
            }

            if (operation.Request.Payloads != null && operation.Request.Payloads.TryGetValue("application/json", out var payload) && payload.ObjectType != null)
            {
                var requestField = new ObjectField
                {
                    Name = nameof(OpenApiOperationActionOptions.Request),
                    Label = "Body",
                    DefaultValue = opts?.Request,
                    ObjectFieldOptions = new ObjectFieldOptions
                    {
                        ObjectType = payload.ObjectType,
                    },
                    Visible = new[] { nameof(OpenApiOperationActionOptions.OperationId) },
                };

                await _objectTypeService.LoadObjectFieldAsync(accountContext, FormName.Edit, requestField, opts.Request, formOptions);

                fields.Add(requestField);
            }
        }
        else
        {
            var operationId = "{{" + nameof(OpenApiOperationActionOptions.OperationId) + "}}";

            fields.Add(
                new ObjectField
                {
                    Name = nameof(OpenApiOperationActionOptions.Parameters),
                    Label = "Parameters",
                    DefaultValue = opts?.Parameters,
                    IsRequired = true,
                    ObjectFieldOptions = new ObjectFieldOptions
                    {
                        ObjectType = "Parameters",
                        AddFormUrls = new Dictionary<string, string>
                        {
                            { "Request", $"/openapi/v1/OperationAction({operationId})/Parameters" }
                        },
                    },
                    Visible = new[] { nameof(OpenApiOperationActionOptions.OperationId) },
                }
            );

            fields.Add(
                new ObjectField
                {
                    Name = nameof(OpenApiOperationActionOptions.Request),
                    Label = "Body",
                    DefaultValue = opts?.Request,
                    IsRequired = true,
                    ObjectFieldOptions = new ObjectFieldOptions
                    {
                        ObjectType = "Request",
                        AddFormUrls = new Dictionary<string, string>
                        {
                            { "Request", $"/openapi/v1/OperationAction({operationId})/Request" }
                        },
                    },
                    Visible = new[] { nameof(OpenApiOperationActionOptions.OperationId) },
                }
            );
        }

        fields.Add(
            new TextField
            {
                Name = nameof(OpenApiOperationActionOptions.Alias),
                Label = "Response Object Alias",
                DefaultValue = opts?.Alias,
            }
        );

        return fields.ToArray();
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, OpenApiOperationActionOptions options)
    {
        var operation = await _connection.Filter<Operation>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, options.OperationId)
            .FirstOrDefaultAsync();

        if (operation == null) throw NotFoundException.New<Operation>(options.OperationId);

        step.Description = operation.Description ?? operation.Summary;

        var outputs = Enumerable.Empty<ActionOutput>();
        foreach (var kvp in operation.Responses)
        {
            var (evt, output) = await AddEventAsync(
                context,
                flow.ObjectType,
                step.CurrentStatusId,
                kvp.Key,
                kvp.Value.Description ?? kvp.Key,
                kvp.Key
            );

            outputs = outputs.Append(output);
        }

        options.Output = outputs.ToArray();
    }
}