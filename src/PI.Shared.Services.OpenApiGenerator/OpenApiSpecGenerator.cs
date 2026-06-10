using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Files;
using PI.Shared.Models.OpenAPI;

namespace PI.Shared.Services.OpenApiGenerator;

public class OpenApiSpecGenerator
{
    private readonly ILogger<OpenApiSpecGenerator> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeIntrospector _introspector;

    public IEntityContext EntityContext
    {
        get => _introspector.Context;
        set => _introspector.Context = value;
    }

    private Dictionary<string, ObjectType> GeneratedObjectTypes { get; } = new();
    private HashSet<string> Dependencies { get; } = new();
    private HashSet<string> Namespaces { get; } = new();

    private string APIResponseError => "api-pi-response-error";
    private string APICondiitonOperator => "api-pi-condition-operator";
    private string APIActionResponse => "DataFormActionResponse";
    private string RunUserActionRequest = "DataFormActionRequest";

    public Func<ObjectType, string> ObjectSchemaNameGenerator { get; set; } = (objectType) => (objectType.ApiName ?? objectType.Name).Replace('.', '-');

    public Func<FormField, string> PropertySchemaNameGenerator { get; set; } = (field) => field.ApiName ?? field.Name;

    public Func<string, ActionEndpoint, string> OperationIdGenerator { get; set; } = (schemaName, endpoint) => endpoint switch
    {
        ActionEndpoint.Get => $"get--{schemaName}",
        ActionEndpoint.Filter => $"filter--{schemaName}",
        ActionEndpoint.DataView => $"data-view--{schemaName}",
        ActionEndpoint.Recent => $"recent--{schemaName}",
        ActionEndpoint.Create => $"create--{schemaName}",
        ActionEndpoint.Delete => $"delete--{schemaName}",
        ActionEndpoint.Update => $"update--{schemaName}",
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null)
    };

    public Func<string, ObjectTypePermission?, string> BodySchemaNameGenerator { get; set; } = (schemaName, permission) => permission switch
    {
        ObjectTypePermission.Create => $"create--{schemaName}--request",
        ObjectTypePermission.Update => $"update--{schemaName}--request",
        ObjectTypePermission.Read => schemaName,
        _ => null,
    };

    public Func<string, string> FilterRequestBodySchemaNameGenerator { get; set; } = objectSchemaName => $"filter--{objectSchemaName}--request";
    public Func<string, string> FilterResponseBodySchemaNameGenerator { get; set; } = objectSchemaName => $"filter--{objectSchemaName}--response";
    public Func<string, string> DataViewResponseBodySchemaNameGenerator { get; set; } = objectSchemaName => $"dataview--{objectSchemaName}--response";

    private List<OpenApiSecurityRequirement> DefaultSecurity => new List<OpenApiSecurityRequirement>
    {
        new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference("ProductionIDP", Document), ["rest"] }
        },
        new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference("TestIDP", Document), ["rest"] }
        }
    };

    public OpenApiDocument Document { get; } = new()
    {
        Components = new OpenApiComponents(),
        Tags = new HashSet<OpenApiTag>(),
        Paths = new OpenApiPaths(),
    };

    public OpenApiSpecGenerator(ILogger<OpenApiSpecGenerator> logger, MongoConnection connection, ObjectTypeIntrospector introspector)
    {
        _logger = logger;
        _connection = connection;
        _introspector = introspector;
    }

    private string GetOperationId(ObjectType objectType, ActionEndpoint endpoint)
    {
        var schemaName = ObjectSchemaNameGenerator(objectType);
        return OperationIdGenerator(schemaName, endpoint);
    }

    private string GetBodySchemaName(ObjectType objectType, ObjectTypePermission? permission)
    {
        var schemaName = ObjectSchemaNameGenerator(objectType);
        return BodySchemaNameGenerator(schemaName, permission);
    }

    public void SetInfo(string title, string description, string version = "0.0.1")
    {
        Document.Info = new OpenApiInfo
        {
            Version = version,
            Title = title,
            Description = description,
            Contact = new OpenApiContact
            {
                Email = "programinterface@programinterface.com",
                Name = "ProgramInterface.com",
                Url = new Uri("https://www.ProgramInterface.com"),
            },
        };
    }

    public void SetServers(params string[] urls)
    {
        Document.Servers = new List<OpenApiServer>(urls.Select(x => new OpenApiServer { Url = x }));
    }

    public void AddSecurity()
    {
        addScheme("ProductionIDP", "idp.inspirenet.cloud");
        addScheme("TestIDP", "idp.fci.cloud");

        Document.Security = DefaultSecurity;

        void addScheme(string name, string baseUrl)
        {
            var scheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OpenIdConnect,
                Name = name,
                Description = "OpenID Connect",
                OpenIdConnectUrl = new Uri($"https://{baseUrl}/.well-known/openid-configuration"),
            };

            Document.AddComponent(scheme.Name, scheme);
        }
    }

    private async Task<OpenApiSchema> BuildObjectSchemaAsync(ObjectType objectType, ObjectTypePermission permission, AddSchemaOptions options)
    {
        // if ((objectType.IsEmbedded || objectType.IsAbstract) && permission != ObjectTypePermission.Read)
        // {
        //     // can't update or create so no point in creating them 
        //     return null;
        // }

        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Title = permission switch
            {
                ObjectTypePermission.Create => $"{objectType.Description ?? objectType.Name}: Create Body",
                ObjectTypePermission.Update => $"{objectType.Description ?? objectType.Name}: Update Body",
                _ => objectType.Description ?? objectType.Name,
            },
            Description = permission switch
            {
                ObjectTypePermission.Create => $"Request Body to Create {objectType.Description ?? objectType.Name}",
                ObjectTypePermission.Update => $"Request Body to Update {objectType.Description ?? objectType.Name}",
                _ => objectType.Description ?? objectType.Name,
            },
        };

        // only generate hierarchy for "Read"
        var includeBaseObject = permission == ObjectTypePermission.Read &&
                                objectType.LoadedBaseObjectType != null &&
                                objectType.LoadedBaseObjectType.Can(options.OverrideRBAC ?? EntityContext, permission);

        if (includeBaseObject)
        {
            AddDependency(objectType.BaseObjectType, "BaseObject", objectType.FullName);

            schema.AllOf = new List<IOpenApiSchema>()
            {
                new OpenApiSchemaReference(ObjectSchemaNameGenerator(objectType.LoadedBaseObjectType), Document),
            };
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();

        var newFields = !includeBaseObject
            ? objectType.Fields?.Values ?? Enumerable.Empty<FieldTemplate>()
            : objectType.Fields.Values
                .Where(x =>
                    objectType.OverriddenFields == null || (x.Field?.Name != null && !objectType.OverriddenFields.ContainsKey(x.Field.Name))
                );

        var fieldPermission = permission switch
        {
            ObjectTypePermission.Read => FieldPermission.Read,
            ObjectTypePermission.Update => FieldPermission.Update,
            ObjectTypePermission.Create => FieldPermission.SetOnCreate,
            _ => throw new BadRequestException("Invalid permission parameter")
        };

        var fieldsToInclude = newFields
            .Where(fieldTemplate => fieldTemplate.RBAC.Can(options.OverrideRBAC ?? EntityContext, fieldPermission))
            .ToArray();

        if (includeBaseObject && fieldsToInclude.IsEmpty())
        {
            // hack, add made up parameter to make api generator happy (at least the dio)
            schema.Properties.Add($"_{objectType.Id.ToString()[0..8]}", new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
            });
        }

        foreach (var fieldTemplate in fieldsToInclude)
        {
            var fieldSchema = await BuildFieldSchemaAsync(fieldTemplate, permission, options);
            if (fieldSchema == null) continue;

            schema.Properties.Add(PropertySchemaNameGenerator(fieldTemplate.Field), fieldSchema);

            // TODO: LIMIT TO THE TOP LEVEL OBJECTS IN A DATAVIEW RESPONSE? 
            // ...

            // add ReferenceField |Name
            if (fieldTemplate.Field is not ReferenceField referenceField || permission != ObjectTypePermission.Read) continue;
            var madeUpField = new FieldTemplate
            {
                RBAC = fieldTemplate.RBAC,
                Indexed = false,
                Field = new TextField
                {
                    Name = $"{referenceField.Name}|Name",
                    ApiName = referenceField.ApiName != null ? $"{referenceField.ApiName}|Name" : null,
                }
            };

            fieldSchema = await BuildFieldSchemaAsync(madeUpField, permission, options);
            if (fieldSchema == null) continue;

            schema.Properties.Add(PropertySchemaNameGenerator(madeUpField.Field), fieldSchema);
        }

        if ((permission != ObjectTypePermission.Read && permission != ObjectTypePermission.Update) || options.AddRequiredFields)
        {
            // do not add required fields to "read" model as will prevent filtering out fields
            // do not add required fields for "update" as it is a patch
            // when adding, required fields with a default value or not marked as required
            schema.Required = fieldsToInclude
                .Where(x => permission switch
                {
                    ObjectTypePermission.Create => x.Field.IsRequired && x.Field.DefaultValue == null && x.InitialValue == null,
                    ObjectTypePermission.Update => x.Field.IsRequired && x.CalculatedValue == null,
                    _ => x.Field.IsRequired,
                })
                .Select(x => PropertySchemaNameGenerator(x.Field))
                .ToHashSet();
        }

        if (permission != ObjectTypePermission.Read)
        {
            return schema;
        }

        // add relation fields
        var allReadable = new Dictionary<string, FieldTemplate>();
        await AddRelationFieldsRecursivelyAsync(objectType, allReadable, options, addChildObjectFields: false);
        foreach (var fieldTemplate in allReadable.Values)
        {
            var fieldSchema = await BuildFieldSchemaAsync(fieldTemplate, permission, options);
            if (fieldSchema != null) schema.Properties.Add(PropertySchemaNameGenerator(fieldTemplate.Field), fieldSchema);
        }

        // calculate discriminator (just for read)
        if (objectType.Discriminator?.Values.Count > 0)
        {
            var fieldName = objectType.Discriminator.Values.First().Conditions.FirstOrDefault(x => x.Operator == Operator.Eq)?.FieldName;
            if (fieldName == null || !objectType.Fields.TryGetValue(fieldName, out var discriminatorField) || !discriminatorField.RBAC.CanRead(options.OverrideRBAC ?? EntityContext))
            {
                _logger.LogInformation("Couldn't determine discriminator field for {ObjectType} or can't read {Field}", objectType.FullName, fieldName);
            }
            else
            {
                var discriminator = new OpenApiDiscriminator
                {
                    PropertyName = discriminatorField.Field.ApiName ?? discriminatorField.Field.Name,
                    Mapping = new Dictionary<string, OpenApiSchemaReference>(),
                };

                foreach (var kvp in objectType.Discriminator)
                {
                    var condition = kvp.Value.Conditions.FirstOrDefault(x => x.Operator == Operator.Eq && x.FieldName == fieldName);
                    if (condition?.Value == null)
                    {
                        _logger.LogInformation("Couldn't determine discriminator value for {Field} in {ObjectType}", fieldName, objectType.FullName);
                        continue;
                    }

                    // only add to discriminator objects that it can read
                    var childObjectType = await GetObjectTypeAsync(kvp.Key, options);
                    if (childObjectType == null) continue;

                    discriminator.Mapping[condition.Value.ToString()] = new OpenApiSchemaReference(ObjectSchemaNameGenerator(childObjectType), Document);
                    AddDependency(kvp.Key, "Discriminator", objectType.FullName);
                }

                if (!discriminator.Mapping.IsEmpty())
                {
                    schema.Discriminator = discriminator;
                }
            }
        }

        return schema;
    }

    private async Task<ObjectType> GetObjectTypeAsync(string objectTypeName, AddSchemaOptions options)
    {
        var objectType = await _introspector.GetObjectTypeAsync(objectTypeName);
        if (objectType == null) return null;

        if (options.OverrideRBAC != null)
        {
            if (options.OverrideRBAC.ProfileId.HasValue)
            {
                objectType.RBAC[options.OverrideRBAC.ProfileId.Value] = ObjectTypePermission.Read;

                foreach (var field in objectType.Fields.Values)
                {
                    field.RBAC[options.OverrideRBAC.ProfileId.Value] = FieldPermission.Read;
                }
            }
            // add read permission to all for profile

            objectType.RBAC[options.OverrideRBAC.Role] = ObjectTypePermission.Read;

            foreach (var field in objectType.Fields.Values)
            {
                field.RBAC[options.OverrideRBAC.Role] = FieldPermission.Read;
            }
        }

        return objectType.CanRead(options.OverrideRBAC ?? EntityContext) ? objectType : null;
    }

    private async Task<IOpenApiSchema> BuildFieldSchemaAsync(FormField f, ObjectTypePermission permission, AddSchemaOptions options)
    {
        switch (f)
        {
            case HiddenField:
            case RelatedObjectsField:
            case LabelField:
                _logger.LogInformation("Ignore {Field}: {Type}", f.Name, f.GetType().FullName);
                return null;

            case ReferenceField field:
                // AddDependency(field.ReferenceFieldOptions?.ObjectType, "ReferenceField", "");
                break;

            case MultiReferenceField field:
                // AddDependency(field.MultiReferenceFieldOptions?.ObjectType, "MultiReferenceField", "");
                break;

            case ObjectField field:
            {
                AddDependency(field.ObjectFieldOptions?.ObjectType, "ObjectField", "");
                var objectSchemaName = field.ObjectFieldOptions.ObjectType != "*" ? await GetSchemaName(field.ObjectFieldOptions.ObjectType, options, permission) : null;
                if (objectSchemaName != null)
                {
                    // just return reference to object
                    return new OpenApiSchemaReference(objectSchemaName, Document);
                }

                break;
            }

            case ChildrenField field:
                AddDependency(field.ChildrenFieldOptions?.ObjectType, "ChildrenField", "");
                break;

            case DictionaryField dictionaryField:
            {
                if (dictionaryField.DictionaryFieldOptions?.ValueField is SelectField selectField)
                {
                    // TODO: create a schema since we know all the keys,
                    // ...
                }

                break;
            }
        }


        var referencedSchemaName = f switch
        {
            // ObjectField objectField => objectField.ObjectFieldOptions.ObjectType != "*" ? await GetSchemaName(objectField.ObjectFieldOptions.ObjectType, options, permission) : null,
            ChildrenField childrenField => await GetSchemaName(childrenField.ChildrenFieldOptions.ObjectType, options, permission),
            _ => null,
        };

        var childSchema = referencedSchemaName == null
            ? null
            : new OpenApiSchemaReference(referencedSchemaName, Document);

        var schema = BuildSchemaForField(f, childSchema);

        // enums
        switch (f)
        {
            case SelectField selectField:
            {
                if (selectField.SelectFieldOptions?.AllowUnknown == true) break;

                var names = new JsonArray();
                var values = new JsonArray();
                var descriptions = new JsonArray();

                var keys = new Dictionary<string, object>();
                foreach (var key in selectField.SelectFieldOptions?.Items.Keys)
                {
                    keys.Add(key.ToString(), key);
                }

                foreach (var strKey in keys.Keys.Order())
                {
                    var key = keys[strKey];
                    var value = selectField.SelectFieldOptions?.Items[key];
                    values.Add(strKey);
                    names.Add(GetSafeEnumName(strKey));
                    descriptions.Add(value?.ToString() ?? strKey);
                }

                schema.Enum = values.ToList<JsonNode>();
                schema.Extensions = new Dictionary<string, IOpenApiExtension>
                {
                    { "x-enum-varnames", new JsonNodeExtension(names) },
                    { "x-enum-descriptions", new JsonNodeExtension(descriptions) },
                };

                break;
            }
        }

        return schema;
    }

    private static OpenApiSchema BuildSchemaForField(FormField field, OpenApiSchemaReference childSchema)
    {
        var schema = new OpenApiSchema
        {
            Title = field.Label ?? field.Name,
            Description = field.Description,
            // schema.Nullable = fieldTemplate.Field switch
            // {
            //     _ => !fieldTemplate.Field.IsRequired,
            // };
            Type = field switch
            {
                TextField or LabelField or PostalCodeField or PhoneField or EmailField or AddressField or PasswordField
                    or DateField or DateTimeField or UrlField => JsonSchemaType.String,
                ObjectField => JsonSchemaType.Object,
                ChildrenField f => f.ChildrenFieldOptions?.KeyType == ChildrenFieldOptions.IndexKeyType
                    ? JsonSchemaType.Array
                    : JsonSchemaType.Object,
                NumberField n => n.NumberFieldOptions?.DecimalPlaces == 0 ? JsonSchemaType.Integer : JsonSchemaType.Number,
                CheckboxField => JsonSchemaType.Boolean,
                ReferenceField => JsonSchemaType.String, // TODO: have to inspect foreignfield name to know
                MultiReferenceField => JsonSchemaType.Array,
                MultiSelectField or TagsField or ArrayField => JsonSchemaType.Array,
                SelectField => JsonSchemaType.String,
                DictionaryField => JsonSchemaType.Object,
                ExpressionField f => f.ExpressionFieldOptions?.ValueField switch
                {
                    // TODO: can probably add all the other types that are backed by a string
                    // ...
                    null or TextField => JsonSchemaType.String, // no matter what the value will be a string
                    _ => JsonSchemaType.Object, // a string or whatever other type
                },
                _ => null,
            },
            Format = field switch
            {
                TextField t => t.TextFieldOptions?.Format,
                EmailField => "email",
                PhoneField => "phone",
                PasswordField => "password",
                DateField => "date",
                DateTimeField => "date-time",
                UrlField => "uri",
                ReferenceField => null, // TODO: have to inspect foreignfield name to know
                _ => null,
            },
            // Note: Reference property removed in v2.0 - use OpenApiSchemaReference for references
            Items = field switch
            {
                ChildrenField f => f.ChildrenFieldOptions?.KeyType == ChildrenFieldOptions.IndexKeyType ? childSchema : null,
                TagsField => new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                },
                MultiReferenceField => new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                },
                MultiSelectField => new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                },
                ArrayField arrayField => arrayField.ArrayFieldOptions?.ValueField switch
                {
                    NumberField numberField => new OpenApiSchema
                    {
                        Type = numberField.NumberFieldOptions?.DecimalPlaces == 0 ? JsonSchemaType.Integer : JsonSchemaType.Number,
                    },
                    TextField textField => new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = textField.TextFieldOptions.Format,
                    },
                    _ => new OpenApiSchema
                    {
                        Type = JsonSchemaType.String
                    },
                },
                _ => null,
            },
            AdditionalProperties = field switch
            {
                ChildrenField f => f.ChildrenFieldOptions?.KeyType == ChildrenFieldOptions.StringKeyType ? childSchema : null,

                // TODO: could we just build the schema for the value field?
                // ...
                DictionaryField dictionaryField => dictionaryField?.DictionaryFieldOptions.ValueField switch
                {
                    TextField => new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                    },
                    _ => null,
                },
                _ => null,
            },
            // Enum = f switch
            // {
            //     SelectField field => field.SelectFieldOptions?.Items?.Keys.ToEnumerableObject()
            //         .Select(x => x.ToString())
            //         .Order()
            //         .Select(x => new OpenApiString(x))
            //         .ToList<IOpenApiAny>(),
            //     _ => null,
            // },
            // Example = f.DefaultValue switch
            // {
            //     string str => new OpenApiString(str),
            //     bool b => new OpenApiBoolean(b),
            //     int i => new OpenApiInteger(i),
            //     double d => new OpenApiDouble(d),
            //     _ => null,
            // }
        };
        return schema;
    }

    private void AddDependency(string objectType, string type, string parentObjectType)
    {
        if (string.IsNullOrEmpty(objectType)) return;

        if (objectType.StartsWith("/"))
        {
            return;
        }

        if (objectType == "*")
        {
            return;
        }

        Dependencies.Add(objectType);
    }

    private async Task<IOpenApiSchema> BuildFieldSchemaAsync(FieldTemplate fieldTemplate, ObjectTypePermission permission, AddSchemaOptions options)
    {
        var schema = await BuildFieldSchemaAsync(fieldTemplate.Field, permission, options);
        if (schema == null) return null;

        if (schema.Format == null && fieldTemplate.InitialValue is "{{new UUID}}")
        {
            // override format
            ((OpenApiSchema)schema).Format = "uuid";
        }

        // does it make sense?
        // schema.ReadOnly = !fieldTemplate.RBAC.CanSetOnCreate(context.EntityContext) &&
        //                   !fieldTemplate.RBAC.CanUpdate(context.EntityContext);
        // schema.WriteOnly = !fieldTemplate.RBAC.CanRead(context.EntityContext);

        return schema;
    }

    /// <summary>
    /// Add operations from namespace  
    /// </summary>
    private async Task SystemExplicitOperationsForNamespaceAsync(string @namespace, AddSchemaOptions opts)
    {
        var ops = await _connection.Filter<Operation>()
            .Eq(x => x.AccountId, _introspector.Context.AccountId)
            .Regex(x => x.Namespace, $"/^{@namespace}./")
            // .Eq(x=>x.Name, "SpaceGet")
            .FindAsync();

        foreach (var operation in ops)
        {
            try
            {
                await SystemExplicitOperationAsync(operation, opts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Operation} failed", operation.OperationId);
            }
        }
    }

    /// <summary>
    /// Add operation
    /// </summary>
    private async Task SystemExplicitOperationAsync(Operation op, AddSchemaOptions opts)
    {
        var tagName = "Generic";
        if (op.Namespace.StartsWith(opts.BaseNamespace))
        {
            var parts = op.Namespace[(opts.BaseNamespace.Length + 1)..].Split('.');
            if (!string.IsNullOrEmpty(parts[0]))
            {
                tagName = parts[0];
            }
        }

        var tag = Document.Tags.FirstOrDefault(x => x.Name == tagName);
        if (tag == null)
        {
            tag = new OpenApiTag { Name = tagName, Description = tagName };
            AddTag(tag);
        }

        var operation = new OpenApiOperation
        {
            Description = op.Name,
            Summary = op.Summary,
            OperationId = op.OperationId,
            Responses = new OpenApiResponses(),
            Tags = new HashSet<OpenApiTagReference> { new OpenApiTagReference(tag.Name, Document) },
            // assume same security for now
            // Security = DefaultSecurity,
        };

        var parameterFields = op.Request.Parameters;
        if (parameterFields == null && !string.IsNullOrEmpty(op.Request.ParametersObjectType))
        {
            // TODO: use schema instead so we can fallback to raw?
            // ...
            var objectType = await _introspector.GetObjectTypeAsync(op.Request.ParametersObjectType);
            parameterFields = objectType?.Fields.ToDictionary(x => x.Key, x => x.Value.Field);
        }

        if (parameterFields != null && op.Request.ParametersPlacement?.Count > 0)
        {
            foreach (var kvp in op.Request.ParametersPlacement)
            {
                if (!parameterFields.TryGetValue(kvp.Key, out var field))
                {
                    throw new Exception($"Could not find field: {kvp.Key}");
                }

                var schema = await BuildFieldSchemaAsync(field, ObjectTypePermission.Read, opts);
                if (schema == null) throw new Exception($"Could not calculate schema for field: {kvp.Key}");

                var required = field.IsRequired || kvp.Value == "Path";
                operation.AddParameter(new OpenApiParameter
                    {
                        Schema = schema,
                        Name = kvp.Key,
                        Description = field.Description,
                        Required = required,
                    }.WithIn(kvp.Value)
                );
            }
        }

        // TODO: only json for now... 
        if (op.Request.Payloads?.TryGetValue("application/json", out var payload) ?? false)
        {
            var objectType = await _introspector.GetObjectTypeAsync(payload.ObjectType);

            // if object type is visible and does not start with "api" use it as a reference
            // otherwise embed
            var embedObject = payload.ObjectType.StartsWith("api.") || !objectType.CanRead(EntityContext);
            IOpenApiSchema schema = embedObject
                ? await BuildObjectSchemaAsync(objectType, ObjectTypePermission.Read, opts)
                : new OpenApiSchemaReference(GetBodySchemaName(objectType, ObjectTypePermission.Read), Document);

            if (schema != null)
            {
                // var schemaName = $"{op.OperationId}RequestBody";
                // GeneratedObjectTypes[objectType.FullName] = objectType;
                // Document.Components.Schemas.TryAdd(schemaName, schema);

                operation.RequestBody = new OpenApiRequestBody
                {
                    Description = objectType.Description,
                    Content = new Dictionary<string, IOpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = schema,
                            // Schema = new OpenApiSchema
                            // {
                            //     Reference = new OpenApiReference
                            //     {
                            //         Type = ReferenceType.Schema,
                            //         Id = schemaName,
                            //     },
                            // }
                        }
                    }
                };
            }
        }

        operation.Responses = new OpenApiResponses();
        foreach (var response in op.Responses ?? [])
        {
            if (response.Value.Payloads == null || !response.Value.Payloads.TryGetValue("application/json", out var respPayload))
            {
                operation.Responses.Add(response.Key, new OpenApiResponse
                {
                    Description = response.Value.Description ?? $"{response.Key} Response for {op.OperationId}",
                });
                continue;
            }

            var objectType = await _introspector.GetObjectTypeAsync(respPayload.ObjectType);

            // if object type is visible and does not start with "api" use it as a reference
            // otherwise embed
            var embedObject = respPayload.ObjectType.StartsWith("api.") || !objectType.CanRead(EntityContext);
            IOpenApiSchema schema = embedObject
                ? await BuildObjectSchemaAsync(objectType, ObjectTypePermission.Read, opts)
                : new OpenApiSchemaReference(GetBodySchemaName(objectType, ObjectTypePermission.Read), Document);

            if (schema == null) continue;
            operation.Responses.Add(response.Key, new OpenApiResponse
            {
                Description = response.Value.Description ?? $"{response.Key} Response for {op.OperationId}",
                Content = new Dictionary<string, IOpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = schema,
                    }
                }
            });
        }

        Document.AddOperation(op.Request.Path, op.Request.Method, operation);
    }

    private void AddTag(OpenApiTag tag)
    {
        if (Document.Tags.Any(x => x.Name == tag.Name)) return;

        Document.Tags.Add(tag);
    }

    /// <summary>
    /// Add (system) schemas, operations used for generic operations
    /// </summary>
    public async Task AddSystemAsync(AddSchemaOptions options)
    {
        await AddSystemObjectTypesAsync(options, [
            nameof(DateRangePreset),
            nameof(Condition),
            // nameof(UIElement),
            nameof(FieldOptions),
            nameof(MenuItem),
            nameof(FormLayout),
            nameof(GridFormRowLayout),
            nameof(GridFormFieldLayout),
            "Field",
            "Form",
            nameof(ReferenceValue),
            "AugmentedTag",
            "FilterCondition",
            "FilterRequest",
            nameof(SelectFieldOptions), // so the types that extend it are included
            nameof(ReferenceFieldOptions), // so the types that extend it are included
            nameof(FormAction),
            nameof(UploadFileOptions),
            nameof(Page),
            nameof(Calculation),
            nameof(DataView),
            "DataFormActionRequest",
            "DataFormActionResponse",
            "SearchMetaData",
        ]);

        if (!options.SkipOperations)
        {
            AddTag(
                new()
                {
                    Name = "Main",
                    Description = "Main Actions",
                }
            );
            AddTag(
                new()
                {
                    Name = "Generic",
                    Description = "Generic Actions",
                }
            );
            AddTag(
                new()
                {
                    Name = "Salesforce",
                    Description = "Salesforce Actions",
                }
            );

            SystemOperationGetNamedFormForObject(options, true);
            SystemOperationGetNamedFormForObject(options, false);
            SystemOperationRunActionForNamedForm(options, true);
            SystemOperationRunActionForNamedForm(options, false);

            SystemOperationGetNamedFormForObject(options, true, "salesforce");
            SystemOperationGetNamedFormForObject(options, false, "salesforce");
            SystemOperationRunActionForNamedForm(options, true, "salesforce");
            SystemOperationRunActionForNamedForm(options, false, "salesforce");

            SystemOperationTagsForObject(options);

            SystemOperationReferenceFieldForObject(options, false);
            SystemOperationReferenceFieldForObject(options, true);

            SystemOperationGetFormForUserAction(options);
            SystemOperationRunUserAction(options);
            SystemOperationGetMenuForUserAction(options);

            // /app/api/Object/{objectTypeName}/DataView
            // ...
        }

        // TODO: process dependencies instead
        // just have to decide whether to exclude some 
        // ...
        Dependencies.Clear();

        if (!options.SkipOperations)
        {
            await SystemExplicitOperationsAsync(options);
        }
    }

    public async Task AddSystemObjectTypesAsync(AddSchemaOptions options, string[] systemObjectTypes)
    {
        // override EntityContext 
        EntityContext = options.OverrideRBAC ?? EntityContext;

        // TODO: have to change UIElement model to include discriminator for, at least, next level
        // or typescript generator will not include it as parent
        // ....

        // TODO: use tag or some other field instead? e.g. Tag = "System"
        // ...
        var names = new List<string>();
        names.AddRange(systemObjectTypes);

        // get names 
        var objectTypes = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, EntityContext.AccountId)
            .In(x => x.BaseObjectType, names)
            .IncludeFields(x => x.Name, x => x.Namespace, x => x.Id, x => x.RBAC)
            .FindAsync();

        foreach (var ot in objectTypes)
        {
            names.Add(ot.FullName);
        }

        foreach (var name in names)
        {
            await ProcessObjectTypeAsync(name, options);
        }
    }

    /// <summary>
    /// Add the operations defined in the options.BaseNamespace
    /// </summary>
    /// <param name="options"></param>
    private async Task SystemExplicitOperationsAsync(AddSchemaOptions options)
    {
        // add system operations
        ObjectSchemaNameGenerator = (objectType) =>
        {
            var name = (objectType.ApiName ?? objectType.FullName);
            if (name.StartsWith(options.BaseNamespace)) name = name[(options.BaseNamespace.Length + 1)..];
            return name.Replace('.', '-');
        };

        await SystemExplicitOperationsForNamespaceAsync(options.BaseNamespace, options);
        await AddDependenciesAsync(options);
    }

    /// <summary>
    /// Add all schemas that the profile has access 
    /// </summary>
    public async Task AddSchemasAsync(AddSchemaOptions options)
    {
        if (EntityContext.Role != EntityRoleId.Profile || !EntityContext.ProfileId.HasValue) throw new BadRequestException("ProfileId required");

        var objectTypes = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, EntityContext.AccountId)
            .BitsAnySet(x => x.RBAC.Permissions[EntityContext.ProfileId.ToString()], 0b111 /*CRU*/)
            .IncludeFields(x => x.Name, x => x.Namespace, x => x.Id, x => x.RBAC)
            .FindAsync();

        foreach (var ot in objectTypes)
        {
            await ProcessObjectTypeAsync(ot.FullName, options);
        }

        await AddDependenciesAsync(options);
    }

    public async Task AddSchemaAsync(AddSchemaOptions options, string objectTypeName)
    {
        if (EntityContext.Role != EntityRoleId.Profile || !EntityContext.ProfileId.HasValue) throw new BadRequestException("ProfileId required");

        var objectTypes = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, EntityContext.AccountId)
            .Eq(x => x.FullName, objectTypeName)
            .BitsAnySet(x => x.RBAC.Permissions[EntityContext.ProfileId.ToString()], 0b111 /*CRU*/)
            .IncludeFields(x => x.Name, x => x.Namespace, x => x.Id, x => x.RBAC)
            .FindAsync();

        foreach (var ot in objectTypes)
        {
            await ProcessObjectTypeAsync(ot.FullName, options);
        }

        await AddDependenciesAsync(options);
    }

    private async Task<string> GetSchemaName(string objectTypeName, AddSchemaOptions options, ObjectTypePermission? permission = null)
    {
        var objectType = await GetObjectTypeAsync(objectTypeName, options);
        if (objectType == null)
        {
            throw new NotFoundException($"{objectTypeName} not found");
        }

        return GetBodySchemaName(objectType, permission);
    }

    private async Task AddSchemasForFilterAsync(ObjectType objectType, AddSchemaOptions options)
    {
        var objectSchemaName = ObjectSchemaNameGenerator(objectType);

        TryAddSchema(FilterRequestBodySchemaNameGenerator(objectSchemaName), await BuildFilterRequestAsync(objectType));
        TryAddSchema(FilterResponseBodySchemaNameGenerator(objectSchemaName), BuildFilterResponse(objectType));

        if (options.Endpoints.Contains(ActionEndpoint.DataView))
        {
            // data view
            TryAddSchema(DataViewResponseBodySchemaNameGenerator(objectSchemaName), BuildDataViewResponse(objectType));
        }
    }

    private async Task AddObjectStatusesAsync(ObjectType objectType, AddSchemaOptions options)
    {
        // since the object may be a "shadow" of another one 
        // find the object used to set the object status instead 
        if (!objectType.TryGetObjectTypeFromObjectStatusField(out var objectTypeName))
        {
            _logger.LogInformation("Couldn't resolve object status field for {ObjectType}", objectType.FullName);
            return;
        }

        var statuses = await _connection.Filter<ObjectStatus>()
            .Eq(x => x.AccountId, EntityContext.AccountId.Value)
            .Eq(x => x.ObjectType, objectTypeName)
            .FindAsync();

        if (statuses.IsEmpty()) return;

        var enumDict = statuses.ToDictionary(x => GetSafeEnumName(x.Name));
        var names = new JsonArray();
        var descriptions = new JsonArray();
        var values = new List<JsonNode>();
        foreach (var key in enumDict.Keys.Order())
        {
            var x = enumDict[key];
            names.Add(GetSafeEnumName(x.Name));
            descriptions.Add(x.Description ?? x.Name);
            values.Add(x.Id.ToString());
        }

        // names.AddRange(statuses.Select(x => new OpenApiString(GetSafeEnumName(x.Name))));
        // descriptions.AddRange(statuses.Select(x => new OpenApiString(x.Description ?? x.Name)));

        var actionEnumSchema = new OpenApiSchema
        {
            Title = $"{objectType.FullName} Statuses",
            Description = $"{objectType.Label ?? objectType.Description ?? objectType.Name} Status Ids",
            Type = JsonSchemaType.String,
            // Enum = statuses.Select(x => new OpenApiString(x.Id.ToString())).ToList<IOpenApiAny>(),
            Enum = values,
            Extensions = new Dictionary<string, IOpenApiExtension>
            {
                { "x-enum-varnames", new JsonNodeExtension(names) },
                { "x-enum-descriptions", new JsonNodeExtension(descriptions) },
            }
        };

        var objectTypeSchemaName = ObjectSchemaNameGenerator(objectType);
        TryAddSchema($"{objectTypeSchemaName}--status", actionEnumSchema);
    }

    private string GetSafeEnumName(string input)
    {
        string name = null;
        var last = '?';
        foreach (var c in input)
        {
            switch (c)
            {
                case >= '0' and <= '9':
                    // can't start with a number
                    name = (name ?? "_") + c;
                    last = '0';
                    break;
                case ' ':
                    name = (name ?? "") + '_';
                    last = '_';
                    break;
                case '_':
                case >= 'A' and <= 'Z':
                    name = (name ?? "") + (last == 'a' ? $"_{c}" : c);
                    last = 'A';
                    break;
                case >= 'a' and <= 'z':
                    // if (name == null)
                    // {
                    //     name = "" + (char)(c - 'a' + 'A');
                    // }
                    // else
                    // {
                    // name += c;
                    // }
                    name = (name ?? "") + c;
                    last = 'a';
                    break;
            }
        }

        // TODO: make it into an option?
        // return name;
        return name?.ToUpperInvariant();
    }

    private async Task BuildUserActionsAsync(ObjectType objectType, AddSchemaOptions options)
    {
        // since the object may be a "shadow" of another one 
        // find the object used to set the flow instead 
        if (!objectType.TryGetObjectTypeFromFlowField(out var objectTypeName))
        {
            _logger.LogInformation("Couldn't resolve objectType from flow field: {ObjectType}", objectType.FullName);
            // return;
            objectTypeName = objectType.FullName;
        }

        var events = await _connection.Filter<EventType>()
            .Eq(x => x.AccountId, EntityContext.AccountId.Value)
            .Eq(x => x.ObjectType, objectTypeName)
            .OfTypeBuilder<EventType, Trigger, UserTrigger>(x => x.Trigger,
                q => q.AnyIn(x => x.ProfileIds, [EntityContext.ProfileId.Value]))
            .FindAsync();

        if (events.IsEmpty()) return;

        foreach (var evt in events)
        {
            if (evt.Trigger is not UserTrigger trigger) continue;
            if (trigger.InputObjectType != null || trigger.OutputObjectType != null)
            {
                // new custom actions
                await OperationForCustomActionAsync(objectType, evt, options);
                continue;
            }

            await OperationGetFormForUserActionAsync(objectType, evt, options);
            OperationRunActionForUserActionForm(objectType, evt, options);
            await OperationStartUserActionAsync(objectType, evt, options);
        }

        AddUserActionEnum(objectType, events);
    }

    private void AddUserActionEnum(ObjectType objectType, List<EventType> events)
    {
        // var names = new OpenApiArray();
        // names.AddRange(events.Select(evt => new OpenApiString(GetSafeEnumName(evt.Name))));
        //
        // var descriptions = new OpenApiArray();
        // descriptions.AddRange(events.Select(evt => new OpenApiString(evt.Trigger.Name)));

        var enumDict = events.ToDictionary(x => GetSafeEnumName(x.Name));
        var names = new JsonArray();
        var descriptions = new JsonArray();
        var values = new List<JsonNode>();
        foreach (var key in enumDict.Keys.Order())
        {
            var x = enumDict[key];
            names.Add(GetSafeEnumName(x.Name));
            descriptions.Add(x.Trigger.Name);
            values.Add(x.Id.ToString());
        }

        // var names = events.Select(x => new OpenApiString(x.Name)).ToArray();
        // add action enum schema
        var actionEnumSchema = new OpenApiSchema
        {
            Title = $"{objectType.FullName} Actions",
            Description = $"{objectType.Label ?? objectType.Description ?? objectType.Name} Event Ids",
            Type = JsonSchemaType.String,
            // Enum = events.Select(x => new OpenApiString(x.Id.ToString())).ToList<IOpenApiAny>(),
            Enum = values,
            Extensions = new Dictionary<string, IOpenApiExtension>
            {
                { "x-enum-varnames", new JsonNodeExtension(names) },
                { "x-enum-descriptions", new JsonNodeExtension(descriptions) },
            }
        };

        var objectTypeSchemaName = ObjectSchemaNameGenerator(objectType);
        TryAddSchema($"{objectTypeSchemaName}--action", actionEnumSchema);
    }

    /// <summary>
    /// Add lookup operation for tags 
    /// </summary>
    private void SystemOperationTagsForObject(AddSchemaOptions options)
    {
        var path = "/app/api/Object/{objectType}/Tags";
        var description = "Get Tags previously used for Object Type";

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        // action
        var operation = new OpenApiOperation
        {
            Description = description,
            OperationId = "GetTagsForObject",
            Parameters = new List<IOpenApiParameter>
            {
                new OpenApiParameter
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "objectType",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                    }
                },
                new OpenApiParameter
                {
                    In = ParameterLocation.Query,
                    Required = false,
                    Name = "partialTag",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                    }
                }
            },
            Responses = new OpenApiResponses(),
            Tags = new HashSet<OpenApiTagReference>
            {
                new OpenApiTagReference("Generic", Document)
            },
            // Security = DefaultSecurity,
        };

        Document.AddSuccessArrayResponse(operation, "AugmentedTag");

        pathItem!.Operations![HttpMethod.Get] = operation;
    }

    /// <summary>
    /// Add lookup action for Reference Fields 
    /// </summary>
    private void SystemOperationReferenceFieldForObject(AddSchemaOptions options, bool withLookup)
    {
        var path = withLookup ? "/app/api/Object/{objectType}/Lookup({fieldName})" : "/app/api/Object/{objectType}/Lookup";
        var description = "Lookup Values for an Object";

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        // action
        var operation = new OpenApiOperation
        {
            Description = description,
            OperationId = withLookup ? "GetReferenceValuesWithField" : "GetReferenceValues",
            Parameters = new List<IOpenApiParameter>(getParameters()),
            RequestBody = new OpenApiRequestBody
            {
                Description = "Request Body",
                Content = new Dictionary<string, IOpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference("FilterRequest", Document)
                    }
                }
            },
            Responses = new OpenApiResponses(),
            Tags = new HashSet<OpenApiTagReference>
            {
                new OpenApiTagReference("Generic", Document)
            },
            // Security = DefaultSecurity,
        };

        Document.AddSuccessArrayResponse(operation, "ReferenceValue");

        pathItem.Operations[HttpMethod.Post] = operation;

        IEnumerable<OpenApiParameter> getParameters()
        {
            yield return new OpenApiParameter
            {
                In = ParameterLocation.Path,
                Required = true,
                Name = "objectType",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                }
            };

            if (withLookup)
            {
                yield return new OpenApiParameter
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "fieldName",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                    }
                };
            }
        }
    }

    /// <summary>
    /// add operation to exec user action for a named form
    /// </summary>
    private void SystemOperationRunActionForNamedForm(AddSchemaOptions options, bool withObject, string service = "app")
    {
        var path = $"/{service}/";
        path += withObject ? "api/Object/{objectType}({objectId})/{formName}/DataForm" : "api/Object/{objectType}/{formName}/DataForm";

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        var suffix = service switch
        {
            "salesforce" => "FromSalesforce",
            _ => string.Empty,
        };

        // action
        var operation = new OpenApiOperation
        {
            Description = withObject ? "Execute action in Object Form" : "Execute action in Object Type Form",
            OperationId = withObject ? $"ExecuteActionInObjectForm{suffix}" : $"ExecuteActionInObjectTypeForm{suffix}",
            Parameters = new List<IOpenApiParameter>(getParameters()),
            RequestBody = new OpenApiRequestBody
            {
                Description = "Request Body",
                Content = new Dictionary<string, IOpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference("DataFormActionRequest", Document)
                    }
                }
            },
            Responses = new OpenApiResponses(),
            Tags = new HashSet<OpenApiTagReference>
            {
                service switch
                {
                    "salesforce" => new OpenApiTagReference("Salesforce", Document),
                    _ => new OpenApiTagReference("Main", Document)
                }
            },
            // Security = DefaultSecurity,
        };

        Document.AddSuccessResponse(operation, "DataFormActionResponse");

        pathItem.Operations[HttpMethod.Post] = operation;

        IEnumerable<OpenApiParameter> getParameters()
        {
            yield return new OpenApiParameter()
            {
                In = ParameterLocation.Path,
                Required = true,
                Name = "objectType",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                }
            };

            yield return new OpenApiParameter()
            {
                In = ParameterLocation.Path,
                Required = true,
                Name = "formName",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                }
            };

            if (withObject)
            {
                yield return new OpenApiParameter()
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "objectId",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "uuid",
                    }
                };
            }
        }
    }

    /// <summary>
    /// Generic operation to get object for object type
    /// </summary>
    private void SystemOperationGetNamedFormForObject(AddSchemaOptions options, bool withObject, string service = "app")
    {
        var path = $"/{service}/";

        path += withObject ? "api/Object/{objectType}({objectId})/{formName}/DataForm" : "api/Object/{objectType}/{formName}/DataForm";

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        // Operations property is already initialized in v2.0

        var suffix = service switch
        {
            "app" => "",
            "salesforce" => "FromSalesforce",
            _ => throw new NotImplementedException($"{service} not supported."),
        };

        // action
        var operation = new OpenApiOperation
        {
            Description = withObject ? "Get Form for Object" : "Get Form for Object Type",
            OperationId = withObject ? $"GetFormForObject{suffix}" : $"GetFormForObjectType{suffix}",
            Parameters = new List<IOpenApiParameter>(getParameters()),
            Responses = new OpenApiResponses(),
            Tags = new HashSet<OpenApiTagReference>
            {
                service switch
                {
                    "salesforce" => new OpenApiTagReference("Salesforce", Document),
                    _ => new OpenApiTagReference("Main", Document)
                }
            },
            // Security = DefaultSecurity,
        };

        Document.AddSuccessResponse(operation, "Form");

        pathItem.Operations[HttpMethod.Get] = operation;

        IEnumerable<OpenApiParameter> getParameters()
        {
            yield return new OpenApiParameter()
            {
                In = ParameterLocation.Path,
                Required = true,
                Name = "objectType",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                }
            };

            yield return new OpenApiParameter()
            {
                In = ParameterLocation.Path,
                Required = true,
                Name = "formName",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                }
            };

            if (withObject)
            {
                yield return new OpenApiParameter()
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "objectId",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "uuid",
                    }
                };
            }
        }
    }

    /// <summary>
    /// Add get form action for specific object type 
    /// </summary>
    private async Task OperationGetFormForObjectTypeAsync(ObjectType objectType, bool withObject, AddSchemaOptions options, string service = "app")
    {
        var path = $"/{service}/";

        path += $"api/Object/{objectType.FullName}";
        path += withObject ? "({objectId})/{formName}/DataForm" : "/{formName}/DataForm";

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        var suffix = service switch
        {
            "app" => "",
            "salesforce" => "FromSalesforce",
            _ => throw new NotImplementedException($"{service} not supported."),
        };

        var optionalParameters = await GetOptionalFormParametersAsync(options);

        // action
        var operation = new OpenApiOperation
        {
            Description = withObject ? "Get Form for Object" : "Get Form for Object Type",
            OperationId = withObject ? $"GetFormFor{objectType.ApiName ?? objectType.SafeFullName}{suffix}" : $"GetFormForType{objectType.ApiName ?? objectType.SafeFullName}{suffix}",
            Parameters = new List<IOpenApiParameter>(getParameters().Concat(optionalParameters)),
            Responses = new OpenApiResponses(),
            Tags = OperationTags(objectType),
            // Security = DefaultSecurity,
        };

        // shared resource 
        Document.AddSuccessResponse(operation, "Form");

        pathItem.Operations[HttpMethod.Get] = operation;

        IEnumerable<OpenApiParameter> getParameters()
        {
            yield return new OpenApiParameter()
            {
                In = ParameterLocation.Path,
                Required = true,
                Name = "formName",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                }
            };

            if (withObject)
            {
                yield return new OpenApiParameter()
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "objectId",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "uuid",
                    }
                };
            }
        }
    }

    /// <summary>
    /// Add get upsert form 
    /// </summary>
    private async Task OperationGetUpsertFormForObjectTypeAsync(ObjectType objectType, AddSchemaOptions options)
    {
        if (objectType.UniqueIndices == null || objectType.UniqueIndices.IsEmpty()) return;

        var service = "app";
        var formName = "Upsert";
        var path = $"/{service}/";

        path += $"api/Object/{objectType.FullName}/{formName}/DataForm";

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        var suffix = service switch
        {
            "app" => "",
            "salesforce" => "FromSalesforce",
            _ => throw new NotImplementedException($"{service} not supported."),
        };

        var parameters = new List<IOpenApiParameter>();
        var keys = new HashSet<string>();
        foreach (var index in objectType.UniqueIndices)
        {
            foreach (var field in index.Fields)
            {
                if (!keys.Add(field)) continue;
                if (!objectType.Fields.TryGetValue(field, out var ft)) continue;
                var schema = await BuildFieldSchemaAsync(ft.Field, ObjectTypePermission.Read, options);
                if (schema == null) continue;
                parameters.Add(new OpenApiParameter
                {
                    In = ParameterLocation.Query,
                    Required = false,
                    Name = field,
                    Schema = schema,
                });
            }
        }

        // action
        var operation = new OpenApiOperation
        {
            Description = "Get Upsert Form for Object Type",
            OperationId = $"GetUpsertFormForType{objectType.ApiName ?? objectType.SafeFullName}{suffix}",
            Parameters = parameters,
            Responses = new OpenApiResponses(),
            Tags = OperationTags(objectType),
            // Security = DefaultSecurity,
        };

        // shared resource 
        Document.AddSuccessResponse(operation, "Form");

        pathItem.Operations[HttpMethod.Get] = operation;
    }

    private void SystemOperationGetFormForUserAction(AddSchemaOptions options)
    {
        var path = "/app/api/Object/{objectType}({objectId})/UserAction({eventId})/DataForm";
        var description = "Get User Action Form for Object";

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        var operationId = "get--user-action-form";

        // action
        var operation = new OpenApiOperation
        {
            Description = description,
            OperationId = operationId,
            Parameters = new List<IOpenApiParameter>
            {
                new OpenApiParameter
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "objectType",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                    }
                },
                new OpenApiParameter
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "objectId",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "uuid",
                    }
                },
                new OpenApiParameter
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "eventId",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "uuid",
                    }
                },
            },
            Responses = new OpenApiResponses(),
            Tags = new HashSet<OpenApiTagReference>
            {
                new OpenApiTagReference("Main", Document)
            },
            // Security = DefaultSecurity,
        };

        Document.AddSuccessResponse(operation, "Form");

        pathItem.Operations[HttpMethod.Get] = operation;
    }

    private void SystemOperationGetMenuForUserAction(AddSchemaOptions options)
    {
        var path = "/app/api/Object/{objectType}({objectId})/Menu";
        var description = "Get Menu for Object";

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        // Operations property is already initialized in v2.0

        var operationId = "get--object--menu";

        // action
        var operation = new OpenApiOperation
        {
            Description = description,
            OperationId = operationId,
            Parameters = new List<IOpenApiParameter>
            {
                new OpenApiParameter
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "objectType",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                    }
                },
                new OpenApiParameter
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "objectId",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "uuid",
                    }
                },
            },
            Responses = new OpenApiResponses(),
            Tags = new HashSet<OpenApiTagReference>
            {
                new OpenApiTagReference("Main", Document)
            },
            // Security = DefaultSecurity,
        };

        Document.AddSuccessResponse(operation, "Menu");

        pathItem.Operations[HttpMethod.Get] = operation;
    }

    private void SystemOperationRunUserAction(AddSchemaOptions options)
    {
        var path = "/app/api/Object/{objectType}({objectId})/UserAction({eventId})/DataForm";
        var description = "Execute User Action for Object";

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        // // schema
        // var schema = new OpenApiSchema
        // {
        //     Type = JsonSchemaType.Object,
        //     Title = "Request body for User Action",
        //     Description = "Request body used to execute User Action for Object",
        // };
        //
        // TryAddSchema(RunUserActionRequest, schema);

        // action
        var operation = new OpenApiOperation
        {
            Description = description,
            OperationId = "run--user-action",
            Parameters = new List<IOpenApiParameter>
            {
                new OpenApiParameter
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "objectType",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                    }
                },
                new OpenApiParameter
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "objectId",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "uuid",
                    }
                },
                new OpenApiParameter
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "eventId",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "uuid",
                    }
                },
            },
            RequestBody = new OpenApiRequestBody
            {
                Description = "Request Body",
                Content = new Dictionary<string, IOpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference(RunUserActionRequest, Document)
                    }
                }
            },
            Responses = new OpenApiResponses(),
            Tags = new HashSet<OpenApiTagReference>
            {
                new OpenApiTagReference("Main", Document)
            },
            // Security = DefaultSecurity,
        };

        Document.AddSuccessResponse(operation, APIActionResponse);

        pathItem.Operations[HttpMethod.Post] = operation;
    }

    private void OperationRunActionForUserActionForm(ObjectType objectType, EventType evt, AddSchemaOptions options)
    {
        if (evt.Trigger is not UserTrigger userTrigger)
        {
            return;
        }

        var path = $"/app/api/Object/{objectType.FullName}" + "({objectId})/UserAction(" + evt.Id + ")/DataForm";
        var description = evt.Description ?? userTrigger.Name ?? evt.Name;

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        var objectTypeSchemaName = ObjectSchemaNameGenerator(objectType);
        var operationId = $"run--{objectTypeSchemaName}--{evt.Name}--action";

        // action
        var operation = new OpenApiOperation
        {
            Description = description,
            OperationId = operationId,
            Parameters = new List<IOpenApiParameter>
            {
                new OpenApiParameter
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "objectId",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "uuid",
                    }
                },
            },
            RequestBody = new OpenApiRequestBody
            {
                Description = "Request Body",
                Content = new Dictionary<string, IOpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference(RunUserActionRequest, Document)
                    }
                }
            },
            Responses = new OpenApiResponses(),
            Tags = OperationTags(objectType),
            // Security = DefaultSecurity,
        };

        Document.AddSuccessResponse(operation, APIActionResponse);

        pathItem.Operations[HttpMethod.Post] = operation;
    }

    /// <summary>
    /// Add operation to run user action for specific Object Type with parameters
    /// </summary>
    private async Task OperationStartUserActionAsync(ObjectType objectType, EventType evt, AddSchemaOptions options)
    {
        if (evt.Trigger is not UserTrigger userTrigger)
        {
            return;
        }

        var path = $"/app/api/Object/{objectType.FullName}" + "({objectId})/UserAction(" + evt.Id + ")";
        var description = evt.Description ?? userTrigger.Name ?? evt.Name;

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        var objectTypeSchemaName = ObjectSchemaNameGenerator(objectType);
        var requestSchema = $"start--{objectTypeSchemaName}--{evt.Name}--request";
        var operationId = $"start--{objectTypeSchemaName}--{evt.Name}";

        // schema
        if (userTrigger.Form?.Fields?.Length > 0)
        {
            var schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Title = $"Request body for {evt.Name}",
                Description = evt.Description,
                Properties = new Dictionary<string, IOpenApiSchema>(),
            };

            var permissions = ObjectTypePermission.Create | ObjectTypePermission.Read | ObjectTypePermission.Update;
            foreach (var field in userTrigger.Form.Fields)
            {
                var fieldSchema = await BuildFieldSchemaAsync(field, permissions, options);
                if (fieldSchema != null) schema.Properties.Add(PropertySchemaNameGenerator(field), fieldSchema);
            }

            TryAddSchema(requestSchema, schema);
        }

        // TODO: what if there is more than one action in the form?
        // add multiple operations?
        // exclude any that starts with # 
        // ...
        // ...

        // action
        var operation = new OpenApiOperation
        {
            Description = description,
            OperationId = operationId,
            Parameters = new List<IOpenApiParameter>
            {
                new OpenApiParameter
                {
                    In = ParameterLocation.Path,
                    Required = true,
                    Name = "objectId",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "uuid",
                    }
                },
            },
            RequestBody = userTrigger.Form?.Fields?.Length > 0
                ? new OpenApiRequestBody
                {
                    Description = "Request Body",
                    Content = new Dictionary<string, IOpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchemaReference(requestSchema, Document)
                        }
                    }
                }
                : null,
            Responses = new OpenApiResponses(),
            Tags = OperationTags(objectType),
            // Security = DefaultSecurity,
        };

        Document.AddSuccessResponse(operation, APIActionResponse);

        pathItem.Operations[HttpMethod.Post] = operation;
    }

    /// <summary>
    /// Add operation to run custom action for specific Object Type with parameters
    /// </summary>
    private async Task OperationForCustomActionAsync(ObjectType objectType, EventType evt, AddSchemaOptions options)
    {
        if (evt.Trigger is not UserTrigger userTrigger)
        {
            return;
        }

        var path = $"/app/api/Object/{objectType.FullName}/CustomAction(" + evt.Id + ")";
        var description = evt.Description ?? userTrigger.Name ?? evt.Name;

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        var objectTypeSchemaName = ObjectSchemaNameGenerator(objectType);
        var requestSchema = $"{objectTypeSchemaName}--{evt.Name}--request";
        var responseSchema = $"{objectTypeSchemaName}--{evt.Name}--response";
        var operationId = $"{objectTypeSchemaName}--{evt.Name}";

        var inputObjectType = userTrigger.InputObjectType != null ? await _introspector.GetObjectTypeAsync(userTrigger.InputObjectType) : null;
        var outputObjectType = userTrigger.OutputObjectType != null ? await _introspector.GetObjectTypeAsync(userTrigger.OutputObjectType) : null;

        // schema
        if (inputObjectType != null)
        {
            var schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Title = inputObjectType.Label,
                Description = inputObjectType.Description,
                Properties = new Dictionary<string, IOpenApiSchema>(),
            };

            var permissions = ObjectTypePermission.Create | ObjectTypePermission.Read | ObjectTypePermission.Update;
            foreach (var kvp in inputObjectType.Fields)
            {
                var field = kvp.Value.Field;
                var fieldSchema = await BuildFieldSchemaAsync(field, permissions, options);
                if (fieldSchema != null) schema.Properties.Add(PropertySchemaNameGenerator(field), fieldSchema);
            }

            TryAddSchema(requestSchema, schema);
        }

        if (outputObjectType != null)
        {
            var schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Title = outputObjectType.Label,
                Description = outputObjectType.Description,
                Properties = new Dictionary<string, IOpenApiSchema>(),
            };

            var permissions = ObjectTypePermission.Create | ObjectTypePermission.Read | ObjectTypePermission.Update;
            foreach (var kvp in outputObjectType.Fields)
            {
                var field = kvp.Value.Field;
                var fieldSchema = await BuildFieldSchemaAsync(field, permissions, options);
                if (fieldSchema != null) schema.Properties.Add(PropertySchemaNameGenerator(field), fieldSchema);
            }

            TryAddSchema(responseSchema, schema);
        }

        // TODO: what if there is more than one action in the form?
        // add multiple operations?
        // exclude any that starts with # 
        // ...
        // ...

        // action
        var operation = new OpenApiOperation
        {
            Description = description,
            OperationId = operationId,
            Parameters = new List<IOpenApiParameter>
            {
            },
            RequestBody = new OpenApiRequestBody
            {
                Description = "Request Body",
                Content = new Dictionary<string, IOpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference(requestSchema, Document)
                    }
                }
            },
            Responses = new OpenApiResponses(),
            Tags = OperationTags(objectType),
            // Security = DefaultSecurity,
        };

        Document.AddSuccessResponse(operation, responseSchema);

        pathItem.Operations[HttpMethod.Post] = operation;
    }

    /// <summary>
    /// Get Form for (specific) user action for (specific) object 
    /// </summary>
    private async Task OperationGetFormForUserActionAsync(ObjectType objectType, EventType evt, AddSchemaOptions options)
    {
        if (evt.Trigger is not UserTrigger userTrigger || userTrigger.Form == null)
        {
            return;
        }

        var path = $"/app/api/Object/{objectType.FullName}" + "({objectId})/UserAction(" + evt.Id + ")/DataForm";
        var description = evt.Description ?? userTrigger.Name ?? evt.Name;

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        var objectTypeSchemaName = ObjectSchemaNameGenerator(objectType);
        var operationId = $"get--{objectTypeSchemaName}--{evt.Name}--form";

        var optionalParameters = await getOptionalParameters();

        // action
        var operation = new OpenApiOperation
        {
            Description = description,
            OperationId = operationId,
            Parameters = new List<IOpenApiParameter>(getParameters().Concat(optionalParameters)),
            Responses = new OpenApiResponses(),
            Tags = OperationTags(objectType),
            // Security = DefaultSecurity,
        };

        Document.AddSuccessResponse(operation, "Form");

        pathItem.Operations[HttpMethod.Get] = operation;

        IEnumerable<OpenApiParameter> getParameters()
        {
            yield return new OpenApiParameter
            {
                In = ParameterLocation.Path,
                Required = true,
                Name = "objectId",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Format = "uuid",
                }
            };
        }

        async Task<List<OpenApiParameter>> getOptionalParameters()
        {
            var list = new List<OpenApiParameter>();
            if (userTrigger.Form?.Fields?.Length > 0)
            {
                foreach (var field in userTrigger.Form.Fields)
                {
                    var schema = await BuildFieldSchemaAsync(field, ObjectTypePermission.Read, options);
                    if (schema == null) continue;

                    // if (field.DefaultValue != null || field is not HiddenField) continue;
                    list.Add(new OpenApiParameter
                    {
                        In = ParameterLocation.Query,
                        Required = false,
                        Name = field.ApiName ?? field.Name,
                        Schema = schema,
                    });
                }
            }

            return list;
        }
    }

    private async Task<List<OpenApiParameter>> GetOptionalFormParametersAsync(AddSchemaOptions options)
    {
        var optionalParameters = new List<OpenApiParameter>();
        foreach (var kvp in _introspector.ReadableFieldsRecursively)
        {
            if (kvp.Value.Visibility switch
                {
                    ObjectTypeIntrospector.FieldVisibility.RelationalField or ObjectTypeIntrospector.FieldVisibility.EmbeddedInRelationField => true,
                    ObjectTypeIntrospector.FieldVisibility.ObjectField => true,
                    _ => false,
                }) continue;

            var schema = await BuildFieldSchemaAsync(kvp.Value.FieldTemplate.Field, ObjectTypePermission.Read, options);
            if (schema != null)
            {
                optionalParameters.Add(new OpenApiParameter()
                {
                    In = ParameterLocation.Query,
                    Required = false,
                    Name = kvp.Key, // use api names?
                    Schema = schema
                });
            }
        }

        return optionalParameters;
    }

    private async Task ProcessObjectTypeAsync(ObjectType objectType, AddSchemaOptions options)
    {
        // _logger.LogWarning("Processing {ObjectType}", objectType.FullName);
        GeneratedObjectTypes[objectType.FullName] = objectType;

        if (!objectType.CanRead(options.OverrideRBAC ?? EntityContext))
        {
            return;
        }

        await _introspector.IntrospectAsync(objectType);

        // only add actions for a profile
        if (EntityContext.ProfileId.HasValue && !options.SkipOperations && !objectType.IsEmbedded)
        {
            if (options.AddUserActionOperations)
            {
                await BuildUserActionsAsync(objectType, options);
            }

            if (options.AddDataForm)
            {
                // get form(s)
                await OperationGetFormForObjectTypeAsync(objectType, true, options);
                await OperationGetFormForObjectTypeAsync(objectType, false, options);

                // get upset form
                await OperationGetUpsertFormForObjectTypeAsync(objectType, options);
            }

            // status
            await AddObjectStatusesAsync(objectType, options);
        }

        var hasAPI = !objectType.IsEmbedded && objectType.CollectionName != null && objectType.CollectionName != "?";

        var schemas = new Dictionary<ObjectTypePermission, OpenApiSchema>();
        foreach (var permission in new[] { ObjectTypePermission.Read, ObjectTypePermission.Update, ObjectTypePermission.Create })
        {
            if (!objectType.Can(options.OverrideRBAC ?? EntityContext, permission))
            {
                continue;
            }

            var schema = await BuildObjectSchemaAsync(objectType, permission, options);

            schemas[permission] = schema;

            var schemaName = GetBodySchemaName(objectType, permission);
            if (schemaName == null) continue;

            if (permission == ObjectTypePermission.Read) GeneratedObjectTypes[objectType.FullName] = objectType;
            TryAddSchema(schemaName, schema);
        }

        if (!hasAPI || options.SkipOperations) return;

        // tag
        if (!string.IsNullOrEmpty(objectType.Namespace))
        {
            if (Namespaces.Add(objectType.Namespace))
            {
                AddTag(new OpenApiTag
                {
                    Name = objectType.Namespace,
                    Description = $"{objectType.Namespace} Namespace",
                });
            }
        }
        else
        {
            AddTag(new OpenApiTag
            {
                Name = objectType.FullName,
                Description = objectType.Description ?? objectType.Name,
            });
        }

        await AddSchemasForFilterAsync(objectType, options);

        foreach (var endpoint in options.Endpoints)
        {
            var permission = endpoint switch
            {
                ActionEndpoint.Get or ActionEndpoint.Filter or ActionEndpoint.Recent or ActionEndpoint.DataView => ObjectTypePermission.Read,
                ActionEndpoint.Create => ObjectTypePermission.Create,
                ActionEndpoint.Update => ObjectTypePermission.Update,
                ActionEndpoint.Delete => ObjectTypePermission.Delete,
                _ => default(ObjectTypePermission?),
            };

            if (!permission.HasValue || !schemas.TryGetValue(permission.Value, out var schema))
            {
                schema = null;
            }

            if (permission.HasValue && !objectType.Can(options.OverrideRBAC ?? EntityContext, permission.Value))
            {
                continue;
            }

            var bodySchemaName = GetBodySchemaName(objectType, permission.Value);

            AddPath(objectType, endpoint, bodySchemaName, schema, options);
        }
    }

    public void AddDefaultSchemas()
    {
        TryAddSchema(APIResponseError, ApiErrorSchema());
        TryAddSchema(APICondiitonOperator, CriteriaOperatorSchema());
        // TryAddSchema(APIActionResponse, ActionResponseSchema());
    }

    private static OpenApiSchema CriteriaOperatorSchema()
    {
        var names = new JsonArray();
        foreach (var item in new[]
                 {
                     "Equal",
                     "GreaterThan",
                     "GreaterThanOrEqual",
                     "In",
                     "LessThan",
                     "LessThanOrEqual",
                     "NotEqual",
                     "NotIn"
                 }.Select(x => JsonValue.Create(x)))
        {
            names.Add(item);
        }

        var descriptions = new JsonArray();
        foreach (var item in new[]
                 {
                     "Equal",
                     "Greater Than",
                     "Greater Than Or Equal",
                     "In",
                     "Less Than",
                     "Less Than Or Equal",
                     "Not Equal",
                     "Not In"
                 }.Select(x => JsonValue.Create(x)))
        {
            descriptions.Add(item);
        }

        var openApiSchema = new OpenApiSchema
        {
            Title = "Condition Operator",
            Description = "System: Condition Operator",
            Type = JsonSchemaType.String,
            Enum = new[]
            {
                "Eq",
                "Gt",
                "Gte",
                "In",
                "Lt",
                "Lte",
                "Ne",
                "Nin"
            }.Select(x => JsonValue.Create(x)).ToList<JsonNode>(),
            Extensions = new Dictionary<string, IOpenApiExtension>
            {
                { "x-enum-varnames", new JsonNodeExtension(names) },
                { "x-enum-descriptions", new JsonNodeExtension(descriptions) },
            }
        };

        return openApiSchema;
    }

    // private static OpenApiSchema ActionResponseSchema()
    // {
    //     var openApiSchema = new OpenApiSchema
    //     {
    //         Title = "Object Action Response",
    //         Description = "Response to initiating an Object Action",
    //         Type = JsonSchemaType.Object,
    //     };
    //
    //     openApiSchema.Properties.Add("message", new OpenApiSchema
    //     {
    //         Title = "Error Message",
    //         Description = "Error Message",
    //         Type = JsonSchemaType.String,
    //     });
    //
    //     openApiSchema.Properties.Add("success", new OpenApiSchema
    //     {
    //         Title = "Success",
    //         Description = "Whether the action was initiated successfully or not",
    //         Type = JsonSchemaType.Boolean,
    //     });
    //
    //     openApiSchema.Properties.Add("nextUrl", new OpenApiSchema
    //     {
    //         Title = "Next URL",
    //         Description = "Url to be launched next",
    //         Type = JsonSchemaType.String,
    //         Format = "uri",
    //     });
    //
    //     openApiSchema.Properties.Add("runId", new OpenApiSchema
    //     {
    //         Title = "Run Id",
    //         Description = "Id of the flow run initiated by this action, if any.",
    //         Type = JsonSchemaType.String,
    //         Format = "uuid",
    //     });
    //
    //     openApiSchema.Properties.Add("ids", new OpenApiSchema
    //     {
    //         Title = "Object Ids",
    //         Description = "Affected Object Ids",
    //         Type = JsonSchemaType.Array,
    //         Items = new OpenApiSchema
    //         {
    //             Type = JsonSchemaType.String,
    //             Format = "uuid",
    //         }
    //     });
    //
    //     return openApiSchema;
    // }

    /// <summary>
    /// Create default api error schema
    /// </summary>
    private static OpenApiSchema ApiErrorSchema()
    {
        var openApiSchema = new OpenApiSchema
        {
            Title = "API Error",
            Description = "System: Response Body on API Error",
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(),
        };

        openApiSchema.Properties.Add("statusCode", new OpenApiSchema
        {
            Title = "Http Status Code",
            Description = "HTTP Response Status Code",
            Type = JsonSchemaType.Integer,
            Minimum = "300",
            Maximum = "600",
        });

        openApiSchema.Properties.Add("message", new OpenApiSchema
        {
            Title = "Error Message",
            Description = "API Error Message",
            Type = JsonSchemaType.String,
        });

        openApiSchema.Properties.Add("success", new OpenApiSchema
        {
            Title = "Success",
            Description = "Whether the request was successful. Always false",
            Type = JsonSchemaType.Boolean,
            Default = false,
        });

        // actor
        // ... 

        return openApiSchema;
    }

    private OpenApiSchema BuildFilterResponse(ObjectType objectType)
    {
        var schema = new OpenApiSchema
        {
            Title = $"{objectType.Description ?? objectType.Name}: Filter Response",
            Description = $"Response Body to filter {objectType.Description ?? objectType.Name}",
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchemaReference(ObjectSchemaNameGenerator(objectType), Document)
        };

        return schema;
    }

    /// <summary>
    /// Data view response  
    /// </summary>
    private OpenApiSchema BuildDataViewResponse(ObjectType objectType)
    {
        var objectSchemaName = ObjectSchemaNameGenerator(objectType);

        var schema = new OpenApiSchema
        {
            Title = $"{objectType.Description ?? objectType.Name}: DataView Response",
            Description = $"Response Body for {objectType.Description ?? objectType.Name} DataView",
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                {
                    "Result", new OpenApiSchemaReference(FilterResponseBodySchemaNameGenerator(objectSchemaName), Document)
                },
                {
                    "Request", new OpenApiSchemaReference(FilterRequestBodySchemaNameGenerator(objectSchemaName), Document)
                },
                {
                    "DataView", new OpenApiSchemaReference("DataView", Document)
                },
                // string Message { get; }
                // string NextUrl { 
            },
        };

        return schema;
    }

    private async Task AddEmbeddedFieldsRecursivelyAsync(ObjectType objectType, Dictionary<string, FieldTemplate> fields, AddSchemaOptions options, HashSet<string> visited = null, string path = null)
    {
        visited ??= [objectType.FullName];

        foreach (var kvp in objectType.Fields)
        {
            if (!kvp.Value.RBAC.CanRead(options.OverrideRBAC ?? EntityContext)) continue;

            var fieldPath = path == null ? kvp.Key : $"{path}|{kvp.Key}";
            fields.Add(fieldPath, kvp.Value);

            var childObjectTypeName = kvp.Value.Field switch
            {
                ObjectField field => field.ObjectFieldOptions?.ObjectType != "*" ? field.ObjectFieldOptions?.ObjectType : null,
                // ReferenceField field => field.ReferenceFieldOptions?.ObjectType,
                _ => null,
            };

            if (childObjectTypeName == null) continue;
            if (!visited.Add(childObjectTypeName)) continue;

            var childObjectType = await GetObjectTypeAsync(childObjectTypeName, options);
            if (childObjectType == null) continue;

            await AddEmbeddedFieldsRecursivelyAsync(childObjectType, fields, options, visited, fieldPath);
        }
    }

    private async Task AddRelationFieldsRecursivelyAsync(ObjectType objectType, Dictionary<string, FieldTemplate> fields, AddSchemaOptions options, HashSet<string> visited = null, string path = null, bool addChildObjectFields = true)
    {
        if (objectType.RelatedObjectTypes == null) return;

        visited ??= [];
        visited.Add(objectType.FullName);

        var relations = objectType.RelatedObjectTypes
            .Where(x => x.RelationType is RelationType.OneToOne or RelationType.OneToMany && x.RBAC.CanRead(options.OverrideRBAC ?? EntityContext))
            .ToArray();

        foreach (var relation in relations)
        {
            var childObjectType = await GetObjectTypeAsync(relation.ObjectType, options);
            if (childObjectType == null) continue;

            var basePath = path == null ? relation.ApiName ?? relation.Name : $"{path}|{relation.ApiName ?? relation.Name}";
            FormField field = relation.RelationType switch
            {
                RelationType.OneToOne => new ObjectField
                {
                    ObjectFieldOptions = new ObjectFieldOptions
                    {
                        ObjectType = relation.ObjectType,
                    }
                },
                RelationType.OneToMany => new ChildrenField
                {
                    ChildrenFieldOptions = new ChildrenFieldOptions
                    {
                        KeyType = ChildrenFieldOptions.IndexKeyType,
                        ObjectType = relation.ObjectType,
                    }
                },
                _ => null,
            };

            if (field != null)
            {
                field.Name = basePath;
                field.Label = relation.Label ?? relation.Name;

                var ft = new FieldTemplate
                {
                    RBAC = new FieldRBAC
                    {
                    },
                    Field = field,
                    Indexed = false,
                };

                if (EntityContext.ProfileId.HasValue)
                {
                    ft.RBAC[EntityContext.ProfileId.Value] = FieldPermission.Read;
                }

                fields.Add(basePath, ft);
            }

            if (addChildObjectFields)
            {
                foreach (var kvp in childObjectType.Fields)
                {
                    if (!kvp.Value.RBAC.CanRead(options.OverrideRBAC ?? EntityContext)) continue;

                    var fieldPath = $"{basePath}|{kvp.Key}";
                    fields.Add(fieldPath, kvp.Value);
                }
            }

            // await AddRelationFieldsRecursivelyAsync(childObjectType, fields, visited, basePath, addChildObjectFields);
        }
    }

    private async Task<OpenApiSchema> BuildFilterRequestAsync(ObjectType objectType)
    {
        // var allReadable = new Dictionary<string, FieldTemplate>();
        // await AddEmbeddedFieldsRecursivelyAsync(objectType, allReadable);
        // // await AddRelationFieldsRecursivelyAsync(objectType, allReadable);
        //
        // var indexed = allReadable
        //     .Where(x => x.Value.Indexed)
        //     .Select(x => x.Key);

        var allReadable = _introspector.ReadableFieldsRecursively;
        var indexed = _introspector.IndexedFieldsRecursively.Values
            .Where(x => x.Visibility switch
            {
                // FOR NOW excludes indexed fields in related objects
                ObjectTypeIntrospector.FieldVisibility.RelationalField => false,
                ObjectTypeIntrospector.FieldVisibility.EmbeddedInRelationField => false,
                _ => true,
            })
            .Select(x => x.ApiAbsoluteName)
            .ToArray();

        if (objectType.IsFullTextSearchable)
        {
            indexed = indexed.Append(Condition.FullTextSearch).ToArray();
        }

        // filterable fields
        var filterableFieldsSchema = new OpenApiSchema
        {
            Title = $"{objectType.Description ?? objectType.Name}: Filterable Fields",
            Description = $"Filterable fields for {objectType.Description ?? objectType.Name}",
            Type = JsonSchemaType.String,
            Enum = indexed
                .Order()
                .Select(x => JsonValue.Create(x))
                .ToArray()
                .ToList<JsonNode>(),
        };

        TryAddSchema($"{ObjectSchemaNameGenerator(objectType)}-indexed-fields", filterableFieldsSchema);

        // all fields
        var allFieldsSchema = new OpenApiSchema
        {
            Title = $"{objectType.Description ?? objectType.Name}: Fields",
            Description = $"Fields for {objectType.Description ?? objectType.Name}",
            Type = JsonSchemaType.String,
            Enum = allReadable.Values
                .Select(x => x.ApiAbsoluteName)
                .Order()
                .Select(x => JsonValue.Create(x))
                .ToArray()
                .ToList<JsonNode>(),
        };

        TryAddSchema($"{ObjectSchemaNameGenerator(objectType)}-fields", allFieldsSchema);

        var schema = new OpenApiSchema
        {
            Title = $"{objectType.Description ?? objectType.Name}: Filter Body",
            Description = $"Request Body to filter {objectType.Description ?? objectType.Name}",
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(),
        };

        schema.Properties.Add("top", new OpenApiSchema
        {
            Title = "Top",
            Description = "Max Number of objects to return",
            Type = JsonSchemaType.Integer,
            Minimum = "0",
            // Maximum =
        });

        schema.Properties.Add("skip", new OpenApiSchema
        {
            Title = "Skip",
            Description = "Number of objects to skip",
            Type = JsonSchemaType.Integer,
            Minimum = "0",
        });

        // schema.Properties.Add("orderBy", filterableFieldsSchema);
        schema.Properties.Add("orderBy", new OpenApiSchemaReference($"{ObjectSchemaNameGenerator(objectType)}-indexed-fields", Document));

        schema.Properties.Add("reverseOrder", new OpenApiSchema
        {
            Title = "Reverse Order",
            Description = "Order By in Reverse Order (Descending)",
            Type = JsonSchemaType.Boolean,
        });

        schema.Properties.Add("view", new OpenApiSchema
        {
            Title = "View",
            Description = "Pre-defined view",
            Type = JsonSchemaType.String,
        });

        schema.Properties.Add("fields", new OpenApiSchema
        {
            Title = "Fields to return",
            Description = "List of fields to return",
            Type = JsonSchemaType.Array,
            // Items = allFieldsSchema,
            Items = new OpenApiSchemaReference($"{ObjectSchemaNameGenerator(objectType)}-fields", Document)
        });

        var conditionSchema = buildConditionSchema();
        TryAddSchema($"{ObjectSchemaNameGenerator(objectType)}-condition", conditionSchema);

        schema.Properties.Add("criteria", new OpenApiSchema
        {
            Title = "Filter Criteria",
            Description = "Conditions to be used to filter objects",
            Type = JsonSchemaType.Array,
            // Items = conditionSchema,
            Items = new OpenApiSchemaReference($"{ObjectSchemaNameGenerator(objectType)}-condition", Document)
        });

        return schema;

        OpenApiSchema buildConditionSchema()
        {
            var cSchema = new OpenApiSchema
            {
                Title = $"{objectType.Description ?? objectType.Name}: Filter Condition",
                Description = $"Field Condition for {objectType.Description ?? objectType.Name}",
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    { "fieldName", new OpenApiSchemaReference($"{ObjectSchemaNameGenerator(objectType)}-indexed-fields", Document) },
                    { "operator", new OpenApiSchemaReference(APICondiitonOperator, Document) },
                    {
                        "value", new OpenApiSchema
                        {
                            Title = "Value",
                            Description = "Value to be used in the filter operation",
                            // Type = JsonSchemaType.Object,
                        }
                    },
                }
            };

            return cSchema;
        }
    }

    /// <summary>
    /// Create schema for object type and add dependencies to context
    /// </summary>
    public async Task ProcessObjectTypeAsync(string objectTypeName, AddSchemaOptions options)
    {
        if (Document!.Components?.Schemas?.ContainsKey(objectTypeName) ?? false)
        {
            _logger.LogInformation("Already includes {Schema}", objectTypeName);
            return;
        }

        if (GeneratedObjectTypes.TryGetValue(objectTypeName, out var objectType))
        {
            _logger.LogInformation("Already generated scheme for {Schema}", objectTypeName);
            return;
        }

        objectType = await GetObjectTypeAsync(objectTypeName, options);

        if (objectType == null)
        {
            _logger.LogError("Couldn't find {ObjectType}", objectTypeName);

            // add to cache so we will not continue trying to find it
            GeneratedObjectTypes[objectTypeName] = null;
            TryAddSchema(objectTypeName, new OpenApiSchema
            {
                Title = objectTypeName,
                Description = "Object Type not found"
            });

            return;
        }

        await ProcessObjectTypeAsync(objectType, options);
    }

    private bool TryAddSchema(string refId, IOpenApiSchema openApiSchema)
    {
        Document!.Components ??= new OpenApiComponents();
        Document!.Components!.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        return Document!.Components!.Schemas.TryAdd(refId, openApiSchema);
    }

    private void AddPath(ObjectType objectType, ActionEndpoint endpoint, string schemaName, OpenApiSchema schema, AddSchemaOptions options)
    {
        if (!(objectType.ApiPaths?.TryGetValue(endpoint.ToString(), out var basePath) ?? false))
        {
            basePath = "/app/";
        }

        var path = endpoint switch
        {
            ActionEndpoint.Create => $"{basePath}api/Object/{objectType.FullName}",
            ActionEndpoint.Filter => $"{basePath}api/Object/{objectType.FullName}/Filter",
            ActionEndpoint.Recent => $"{basePath}api/Object/{objectType.FullName}/Recent",
            ActionEndpoint.DataView => $"{basePath}api/Object/{objectType.FullName}/DataView",
            _ => $"{basePath}api/Object/{objectType.FullName}" + "({objectId})",
        };

        var method = endpoint switch
        {
            ActionEndpoint.Create => HttpMethod.Post,
            ActionEndpoint.Update => HttpMethod.Patch,
            ActionEndpoint.Get => HttpMethod.Get,
            ActionEndpoint.Delete => HttpMethod.Delete,
            ActionEndpoint.Filter or ActionEndpoint.Recent or ActionEndpoint.DataView => HttpMethod.Post,
            _ => throw new BadRequestException("Invalid permission"),
        };

        var description = endpoint switch
        {
            ActionEndpoint.Create => $"Create {objectType.Label ?? objectType.Description ?? objectType.Name}",
            ActionEndpoint.Update => $"Update {objectType.Label ?? objectType.Description ?? objectType.Name}",
            ActionEndpoint.Get => $"Get {objectType.Label ?? objectType.Description ?? objectType.Name}",
            ActionEndpoint.Delete => $"Delete {objectType.Label ?? objectType.Description ?? objectType.Name}",
            ActionEndpoint.Filter => $"Filter {objectType.LabelPlural ?? objectType.Description ?? objectType.Name}",
            ActionEndpoint.Recent => $"Recent {objectType.LabelPlural ?? objectType.Description ?? objectType.Name}",
            ActionEndpoint.DataView => $"DataView {objectType.LabelPlural ?? objectType.Description ?? objectType.Name}",
            _ => throw new BadRequestException("Invalid permission"),
        };

        if (!Document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>(),
            };

            Document.Paths[path] = pathItem;
        }

        var operation = new OpenApiOperation
        {
            Description = description,
            OperationId = GetOperationId(objectType, endpoint),
            Parameters = endpoint switch
            {
                ActionEndpoint.Create => null,
                ActionEndpoint.Filter or ActionEndpoint.Recent or ActionEndpoint.DataView => null,
                ActionEndpoint.Update => new List<IOpenApiParameter>
                {
                    new OpenApiParameter
                    {
                        In = ParameterLocation.Path,
                        Required = true,
                        Name = "objectId",
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Format = "uuid",
                        }
                    }
                },
                _ => new List<IOpenApiParameter>
                {
                    new OpenApiParameter
                    {
                        In = ParameterLocation.Path,
                        Required = true,
                        Name = "objectId",
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Format = "uuid",
                        }
                    },
                    new OpenApiParameter
                    {
                        In = ParameterLocation.Query,
                        Required = false,
                        Name = "fields",
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Array,
                            Items = new OpenApiSchemaReference($"{ObjectSchemaNameGenerator(objectType)}-fields", Document)
                        }
                    },
                    new OpenApiParameter
                    {
                        In = ParameterLocation.Header,
                        Required = false,
                        Name = endpoint == ActionEndpoint.Get ? "If-Modified-Since" : "If-Unmodified-Since",
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Format = "date-time",
                        }
                    },
                }
            },
            RequestBody = endpoint switch
            {
                ActionEndpoint.Delete or ActionEndpoint.Get => null,
                ActionEndpoint.Filter or ActionEndpoint.Recent or ActionEndpoint.DataView => new OpenApiRequestBody
                {
                    Description = "Filter Criteria",
                    Content = new Dictionary<string, IOpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchemaReference(FilterRequestBodySchemaNameGenerator(ObjectSchemaNameGenerator(objectType)), Document)
                        }
                    }
                },
                ActionEndpoint.Update => new OpenApiRequestBody
                {
                    Description = "Properties to be updated",
                    Content = new Dictionary<string, IOpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = schemaName == null
                                ? schema
                                : new OpenApiSchemaReference(schemaName, Document)
                        }
                    }
                },
                ActionEndpoint.Create => new OpenApiRequestBody
                {
                    Description = "Object to be created",
                    Content = new Dictionary<string, IOpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = schemaName == null
                                ? schema
                                : new OpenApiSchemaReference(schemaName, Document)
                        }
                    }
                },
            },
            Responses = new OpenApiResponses(),
            Tags = OperationTags(objectType),
            // Security = DefaultSecurity,
        };

        pathItem.Operations[method] = operation;

        foreach (var kvp in responses())
        {
            operation.Responses.Add(kvp.Key, kvp.Value);
        }

        return;

        IEnumerable<KeyValuePair<string, OpenApiResponse>> responses()
        {
            var success = endpoint switch
            {
                ActionEndpoint.Filter or ActionEndpoint.Recent => new OpenApiResponse
                {
                    Description = "OK",

                    // TODO: ... 
                    Content = new Dictionary<string, IOpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchemaReference(FilterResponseBodySchemaNameGenerator(ObjectSchemaNameGenerator(objectType)), Document)
                        }
                    }
                },
                ActionEndpoint.DataView => new OpenApiResponse
                {
                    Description = "OK",

                    // TODO: ... 
                    Content = new Dictionary<string, IOpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchemaReference(DataViewResponseBodySchemaNameGenerator(ObjectSchemaNameGenerator(objectType)), Document)
                        }
                    }
                },
                ActionEndpoint.Update or ActionEndpoint.Create => new OpenApiResponse
                {
                    Description = "OK",

                    // TODO: ... 
                    Content = new Dictionary<string, IOpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchemaReference("DataFormActionResponse", Document)
                        }
                    }
                },
                // single record
                _ => new OpenApiResponse
                {
                    Description = "OK",
                    Content = new Dictionary<string, IOpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchemaReference(ObjectSchemaNameGenerator(objectType), Document)
                        }
                    }
                }
            };

            yield return new KeyValuePair<string, OpenApiResponse>("200", success);

            // based on cache
            switch (endpoint)
            {
                case ActionEndpoint.Create:
                    yield return new KeyValuePair<string, OpenApiResponse>("409", new OpenApiResponse
                    {
                        Description = "Conflict",
                        Content = new Dictionary<string, IOpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Schema = new OpenApiSchemaReference(APIResponseError, Document)
                            }
                        }
                    });
                    break;

                case ActionEndpoint.Update:
                case ActionEndpoint.Delete:
                    yield return new KeyValuePair<string, OpenApiResponse>("409", new OpenApiResponse
                    {
                        Description = "Conflict",
                        Content = new Dictionary<string, IOpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Schema = new OpenApiSchemaReference(APIResponseError, Document)
                            }
                        }
                    });
                    yield return new KeyValuePair<string, OpenApiResponse>("400", new OpenApiResponse
                    {
                        Description = "Bad Request",
                        Content = new Dictionary<string, IOpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Schema = new OpenApiSchemaReference(APIResponseError, Document)
                            }
                        }
                    });
                    break;
                case ActionEndpoint.Get:
                    yield return new KeyValuePair<string, OpenApiResponse>("304", new OpenApiResponse
                    {
                        Description = "Not Modified",
                    });
                    break;
            }
        }
    }

    private ISet<OpenApiTagReference> OperationTags(ObjectType objectType)
    {
        if (!string.IsNullOrWhiteSpace(objectType.Namespace))
        {
            return new HashSet<OpenApiTagReference>
            {
                new OpenApiTagReference(objectType.Namespace, Document)
            };
        }
        else
        {
            return new HashSet<OpenApiTagReference>
            {
                new OpenApiTagReference(objectType.FullName, Document)
            };
        }
    }

    public async Task AddDependenciesAsync(AddSchemaOptions options)
    {
        // process dependencies
        while (true)
        {
            var pending = Dependencies.Except(GeneratedObjectTypes.Keys).ToArray();
            if (pending.Length < 1) break;

            foreach (var ot in pending)
            {
                await ProcessObjectTypeAsync(ot, options);
            }
        }
    }

    public class AddSchemaOptions
    {
        public IEntityContext OverrideRBAC { get; init; }
        public bool AddRequiredFields { get; init; }
        public string BaseNamespace { get; init; }
        public bool SkipOperations { get; set; }
        public bool AddUserActionOperations { get; set; }
        public bool AddDataForm { get; set; } = true;

        public ActionEndpoint[] Endpoints { get; init; } =
        [
            ActionEndpoint.Filter,
            ActionEndpoint.Create,
            ActionEndpoint.Update,
            ActionEndpoint.Delete,
            ActionEndpoint.Recent,
            ActionEndpoint.DataView
        ];
    }
}