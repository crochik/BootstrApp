using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Crochik.Mongo;
using MCP.Services;
using McpServer.Models;
using McpServer.Tools.Attributes;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Newtonsoft.Json;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services.OpenApiGenerator;

namespace McpServer.Tools;

public class ManageObjectTypeTools(
    ILogger<ManageObjectTypeTools> logger,
    IServiceScopeFactory scopeFactory,
    MongoConnection connection,
    BootstrAppService appService
)
{
    [McpTool(
        Name = "add_object_class",
        Description = "Add object class/type to be used by the application. ",
        ExamplePrompts =
        [
            "Create Object Type for my app",
            "Define class"
        ])
    ]
    public async Task<string> AddObjectAsync(
        IEntityContext context,
        [McpParameter(Description = "App Name", Required = true)]
        string appName,
        [McpParameter(Description = "Full Class Name (including namespace)", Required = true)]
        string fullObjectType,
        [McpParameter(Description = "Optional Base Object Class Full Name (including namespace)", Required = false)]
        string baseObjectType,
        [McpParameter(Description = "Whether this is a top level object or Embedded.", Required = true)]
        ObjectTypeType type,
        [McpParameter(Description = "List field names that should be indexed. Only indexed fields can be used to filter results.", Required = true)]
        string[] indexedFields,
        [McpParameter(Description = "Reference Field. List of fields that reference a foreign object/field.", Required = false)]
        ForeignKey[] foreignKeys,
        [McpParameter(Description = "Json schema defining the object", Required = true)]
        object schema
    )
    {
        var app = await appService.GetAppAsync(context, appName);
        if (app == null) throw new McpToolException($"{appName} not found or hasn't been initialized");

        var accountContext = new AccountContext(app.AccountId).WithActorFrom(context);

        var appNameSpace = app.ObjectsNamespace;
        if (!fullObjectType.StartsWith(appNameSpace)) throw new McpToolException($"Object should be under namespace: {appNameSpace}");
        if (!string.IsNullOrEmpty(baseObjectType) && !baseObjectType.StartsWith(appNameSpace)) throw new McpToolException($"Base Object should also be under namespace: {appNameSpace}");

        logger.LogInformation("Add: {ObjectType}: {BaseObjectType} - {Type}", fullObjectType, baseObjectType, type);

        var objectType = await appService.GetObjectTypeAsync(accountContext, fullObjectType);
        if (objectType != null)
        {
            throw new McpToolException($"There is already one class with the name: {fullObjectType}");
        }

        if (schema is not JsonElement ele)
        {
            logger.LogInformation("Schema: {Schema}", JsonConvert.SerializeObject(schema, Formatting.Indented));
            throw new McpToolException($"Invalid or missing schema: {fullObjectType}");
        }

        logger.LogInformation("Schema: {Schema}", ele.ToString());

        var json = ele.GetRawText();
        var doc = new OpenApiDocument();
        var parsedSchema = OpenApiModelFactory.Parse<OpenApiSchema>(
            json,
            OpenApiSpecVersion.OpenApi3_1,
            doc,
            out OpenApiDiagnostic diagnostic,
            format: OpenApiConstants.Json
        );

        using var scope = scopeFactory.CreateScope();
        var parser = scope.ServiceProvider.GetRequiredService<OpenApiParser>();

        var otSchema = parser.BuildObjectType($"{fullObjectType}", parsedSchema);
        objectType = otSchema?.ObjectType;
        if (objectType == null) throw new McpToolException("Failed to parse schema");

        var indexed = (indexedFields ?? []).ToHashSet();

        objectType.RBAC[app.ProfileId] = ObjectTypePermission.Read | ObjectTypePermission.Create | ObjectTypePermission.Update | ObjectTypePermission.Delete;
        foreach (var field in objectType.Fields)
        {
            field.Value.RBAC[app.ProfileId] = FieldPermission.Read | FieldPermission.SetOnCreate | FieldPermission.Update;
            field.Value.Indexed = indexed.Contains(field.Key);

            if (field.Key == "_id")
            {
                field.Value.InitialValue = "{{new UUID}}";
            }

            if (field.Value.Field.DefaultValue != null)
            {
                var defaultValue = field.Value.Field.DefaultValue switch
                {
                    JsonElement e => e.ValueKind switch
                    {
                        JsonValueKind.Undefined => null,
                        JsonValueKind.Object => null, // TODO: ... 
                        // JsonValueKind.Array => null,
                        JsonValueKind.String => e.GetString(),
                        JsonValueKind.Number => e.TryGetInt16(out var i) ? i : e.TryGetInt64(out var l) ? l : e.TryGetDecimal(out var d) ? d : e.TryGetDouble(out var d1) ? d1 : null,
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => null,
                    },
                    JsonValue jv => jv.GetValueKind() switch
                    {
                        JsonValueKind.Undefined => null,
                        JsonValueKind.String => jv.GetValue<string>(),
                        JsonValueKind.Number => jv.TryGetValue<int>(out var i) ? i : jv.TryGetValue<long>(out var l) ? l : jv.TryGetValue<decimal>(out var d) ? d :jv.TryGetValue<double>(out var d1) ? d1 : null,
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        // JsonValueKind.Object => null, // TODO: ... 
                        // JsonValueKind.Array => jv.get
                        // JsonValueKind.Null => null,
                        _ => null,
                    },
                    _ => field.Value.Field.DefaultValue,
                };
                
                Console.WriteLine($"has default value: {field.Value.Field.DefaultValue.GetType()} => {defaultValue} ({defaultValue?.GetType()})");

                field.Value.Field.DefaultValue = defaultValue;
            }
        }

        objectType.AccountId = accountContext.AccountId.Value;
        objectType.EntityId = accountContext.AccountId.Value;
        objectType.LastActor = accountContext.Actor();
        objectType.Tags = ["BOOTSTRAPP.cloud"];
        objectType.BaseObjectType = baseObjectType;
        objectType.IsEmbedded = type == ObjectTypeType.Embedded;
        objectType.CollectionName = objectType.IsEmbedded ? "[EMBEDDED]" : $"{fullObjectType}";
        objectType.DatabaseName = "BOOTSTRAPP";

        if (!objectType.IsEmbedded)
        {
            // top level 
            objectType.Constraints ??= new Dictionary<string, Criteria>();
            objectType.Constraints[nameof(EntityRoleId.Account)] = new Criteria
            {
            };

            // TODO: add placeholder flow
            // ...

            // TODO: should set it or just get the first when none is defined (assume just one) 
            // objectType.InitialFlowId = 

            // TODO: should it always add "default" fields:  AccountId, FlowId, ObjectStatusId, ... 
            // ...
        }

        if (foreignKeys?.Length > 0)
        {
            foreach (var foreignKey in foreignKeys)
            {
                // replace field with Reference
                if (objectType.Fields.TryGetValue(foreignKey.LocalFieldName, out var field))
                {
                    var referenceField = new ReferenceField
                    {
                        Name = field.Field.Name,
                        Description = field.Field.Description,
                        Label = field.Field.Label,
                        IsRequired = field.Field.IsRequired,
                        DefaultValue = field.Field.DefaultValue,
                        ReferenceFieldOptions = new ReferenceFieldOptions
                        {
                            ObjectType = foreignKey.ReferenceObjectType,
                            ForeignFieldName = foreignKey.ReferenceFieldName,
                        },
                    };

                    field.Field = referenceField;
                }
                else
                {
                    throw new McpToolException($"Trying to add constraint to a field that was not defined: {foreignKey.LocalFieldName}");
                }
            }

            // add relation
            objectType.RelatedObjectTypes = foreignKeys
                .Select(x => new RelatedObjectType
                {
                    ObjectType = x.ReferenceObjectType,
                    Name = x.ReferenceObjectType,
                    Options = new RelatedObjectTypeOptions
                    {
                    },
                    Conditions =
                    [
                        Condition.Eq(x.ReferenceFieldName, "{{" + x.LocalFieldName + "}}")
                    ],
                    RelationType = RelationType.OneToOne,
                })
                .ToArray();
        }

        await connection.InsertAsync(objectType);

        return $"{fullObjectType} Class added to the application";
    }

    [McpTool(
        Name = "register_action",
        Description = "Register Object Class Action. ",
        ExamplePrompts =
        [
            "Add Action to Class",
            "Register Action for Object type"
        ])
    ]
    public async Task<string> RegisterActionAsync(
        IEntityContext context,
        [McpParameter(Description = "Fullname of object class to register this action for. Fullname is the the {namespace}.{object_name}", Required = true)]
        string fullObjectType,
        [McpParameter(Description = "Fullname of action input class to be used as the body when executing this action. Fullname is the the {namespace}.{object_name}", Required = false)]
        string actionInputObjectType,
        [McpParameter(Description = "Fullname of action output class to be used as the body when executing this action. Fullname is the the {namespace}.{object_name}", Required = false)]
        string actionOutputObjectType,
        [McpParameter(Description = "Action Name", Required = true)]
        string actionName,
        [McpParameter(Description = "Action Description", Required = true)]
        string actionDescription,
        [McpParameter(Description = "Action Scope (one instance, multiple instances/bulk, class)", Required = true)]
        ActionScope scope,
        [McpParameter(Description = "Action Summary", Required = true)]
        string actionSummary
    )
    {
        var parts = fullObjectType.Split('.');

        var app = await appService.GetAppAsync(context, parts[1]);
        if (app == null) throw new McpToolException($"{parts[1]} not found or hasn't been initialized");

        var accountContext = new AccountContext(app.AccountId).WithActorFrom(context);

        logger.LogInformation("Add to {FullObjectType}: {ActionInputObjectType} => {ActionOutputObjectType}", fullObjectType, actionInputObjectType, actionOutputObjectType);

        var eventType = new EventType
        {
            Id = Guid.NewGuid(),
            AccountId = accountContext.AccountId.Value,
            EntityId = accountContext.AccountId.Value,
            Name = actionName,
            Description = actionSummary, // short
            Summary = actionDescription, // long, what it does
            ObjectType = fullObjectType,
            CreatedOn = DateTime.UtcNow,
            LastActor = accountContext.Actor(),
            Trigger = new UserTrigger
            {
                Name = actionName,
                ProfileIds = [app.ProfileId],
                InputObjectType = actionInputObjectType,
                OutputObjectType = actionOutputObjectType,
                AllowNone = scope == ActionScope.Class,
                AllowMultiple = scope == ActionScope.Bulk,
                // ...
                // Message = actionScript
            },
        };

        await connection.InsertAsync(eventType);

        return $"{fullObjectType}, register action {actionInputObjectType} => {actionOutputObjectType}";
    }

    // [McpTool(
    //     Name = "get_class_schema",
    //     Description = "Get Schema for Object Class. It will return the jsonschema for it ",
    //     ExamplePrompts =
    //     [
    //         "Get Object type schema",
    //         "Get Class Schema",
    //         "Get Properties for Object type"
    //     ])
    // ]
    // public async Task<string> GetObjectTypeSchemaAsync(
    //     IEntityContext context,
    //     [McpParameter(Description = "Fullname of object class to get schema for. Fullname is the the {namespace}.{object_name}", Required = true)]
    //     string fullObjectType
    // )
    // {
    //     var objectType = await objectTypeService.GetAsync(context, fullObjectType);
    //     if (objectType == null) throw new McpToolException($"{fullObjectType} does not exist");
    //
    //     return "$"
    // }

    [McpTool(
        Name = "update_action_script",
        Description = "Update action script",
        ExamplePrompts =
        [
            "Update action script",
            "Change the action script"
        ])
    ]
    public async Task<string> UpdateActionScriptAsync(
        IEntityContext context,
        [McpParameter(Description = "Fullname of object class to register this action for. Fullname is the the {namespace}.{object_name}", Required = true)]
        string fullObjectType,
        [McpParameter(Description = "Action Name", Required = true)]
        string actionName,
        [McpParameter(Description = "Action Script using Api script grammar, rules and functions.", Required = true)]
        string actionScript
    )
    {
        var parts = fullObjectType.Split('.');

        var app = await appService.GetAppAsync(context, parts[1]);
        if (app == null) throw new McpToolException($"{parts[1]} not found or hasn't been initialized");

        // TODO: update action in the flow directly, instead 
        // ...
        
        var accountContext = new AccountContext(app.AccountId).WithActorFrom(context);
        var update = await connection.Filter<EventType>()
            .Eq(x => x.AccountId, accountContext.AccountId.Value)
            .Eq(x => x.EntityId, accountContext.AccountId.Value)
            .Eq(x => x.Name, actionName)
            .Eq(x => x.ObjectType, fullObjectType)
            .Update
            .Set("Script", actionScript)
            .Set(x => x.LastActor, accountContext.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        if (update == null) throw new McpToolException($"Couldn't update script for {actionName}");
        return $"Script for {actionName} on {fullObjectType} successfully updated";
    }

    [McpTool(
        Name = "get_namespaces",
        Description = "Get all visible object type namespaces. Namespaces are hierarchal and delimited by dots.",
        ExamplePrompts =
        [
            "What namespaces are defined for my object classes?",
            "How are my object classes organized"
        ])
    ]
    public async Task<string[]> GetNamespaces(
        IEntityContext context
    )
    {
        var cursor = await connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            // .Regex(x => x.Namespace, $"/^app./")
            .DistinctAsync<string>("Namespace");

        var ret = new List<string>();
        while (await cursor.MoveNextAsync())
        {
            foreach (var row in cursor.Current)
            {
                ret.Add(row);
            }
        }

        return ret.ToArray();
    }

    [McpTool(
            Name = "get_object_classes",
            Description = "Get all visible Object Classes defined under a namespace, or at the system level if namespace parameter is omitted.",
            ExamplePrompts =
            [
                "What classes have been defined?",
                "What are the different object types in this app namespace?"
            ],
            StructuredOutput = true
        )
    ]
    public async Task<NamespaceResponse> GetObjectClasses(
        IEntityContext context,
        [McpParameter(Description = "Namespace")]
        string @namespace
    )
    {
        var query = connection.Filter<ObjectType>()
                .Eq(x => x.AccountId, context.AccountId.Value)
            ;

        if (!string.IsNullOrWhiteSpace(@namespace))
        {
            if (!@namespace.StartsWith("app.")) throw new McpToolException($"{@namespace} access forbidden");
            // query.Eq(x => x.Namespace, @namespace);
            query.Regex(x => x.Namespace, $"/^{@namespace.Replace(".", "\\.")}/");
        }
        else
        {
            query.Regex(x => x.Namespace, $"/^app\\./");
        }

        query
            .IncludeFields(
                x => x.Namespace,
                x => x.Name,
                x => x.Description,
                x => x.Label,
                x => x.LabelPlural
            )
            .SortAsc(x => x.FullName);

        var list = await query.FindAsync();

        return new NamespaceResponse
        {
            Namespace = @namespace,
            ObjectClasses = list.Select(x => new ObjectClass
            {
                Namespace = x.Namespace,
                Name = x.FullName,
                Description = x.Description,
                BasedObjectType = x.BaseObjectType,
            }).ToArray(),
        };
    }

    public enum ObjectTypeType
    {
        TopLevel,
        Embedded,
    }

    public class NamespaceResponse
    {
        [JsonPropertyName("objectClasses")] public required ObjectClass[] ObjectClasses { get; set; }

        [JsonPropertyName("namespace")] public string? Namespace { get; set; }
    }

    public class ObjectClass
    {
        [JsonPropertyName("namespace")] public string? Namespace { get; set; }
        [JsonPropertyName("name")] public required string Name { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("basedObjectType")] public string? BasedObjectType { get; set; }
    }

    public class ForeignKey
    {
        public required string LocalFieldName { get; set; }
        public required string ReferenceObjectType { get; set; }
        public required string ReferenceFieldName { get; set; }
    }

    public enum ActionScope
    {
        Instance,
        Bulk,
        Class
    }
}