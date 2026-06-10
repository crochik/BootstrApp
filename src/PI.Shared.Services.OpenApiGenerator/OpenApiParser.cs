using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using PI.Shared.Models.OpenAPI;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using Operation = PI.Shared.Models.OpenAPI.Operation;
using Path = PI.Shared.Models.OpenAPI.Path;

namespace PI.Shared.Services.OpenApiGenerator;

// https://github.com/OAI/OpenAPI-Specification/blob/main/versions/3.1.0.md
public class OpenApiParser
{
    private readonly ILogger<OpenApiParser> _logger;

    public OpenApiDocument OpenApiDocument { get; private set; }
    public string Namespace { get; set; }
    public OpenApiDiagnostic Diagnostic { get; private set; }
    public Dictionary<string, Schema> ObjectTypes { get; } = new();
    public Dictionary<string, Response> Responses { get; } = new();
    public Dictionary<string, Path> Paths { get; } = new();
    public Dictionary<string, object> Components { get; } = new();
    public Dictionary<string, Operation> Operations { get; } = new();


    public List<MissingRef> MissingRefs { get; set; } = new();
    public Guid EntityId { get; set; }
    public Guid AccountId { get; set; }

    public ObjectTypeRBAC DefaultObjectTypeRbac { get; set; } = new ObjectTypeRBAC
    {
        [EntityRoleId.Account] = ObjectTypePermission.Read | ObjectTypePermission.Create | ObjectTypePermission.Update,
        [EntityRoleId.Admin] = ObjectTypePermission.Read | ObjectTypePermission.Create | ObjectTypePermission.Update
    };

    public OpenApiParser(ILogger<OpenApiParser> logger)
    {
        _logger = logger;
    }

    public object BuildSchemaComponent(string name, OpenApiSchema schema)
    {
        if (schema.Type == JsonSchemaType.Object)
        {
            var objectType = BuildObjectType($"{Namespace}.{name}", schema);
            if (objectType == null)
            {
                _logger.LogError("Failed to create {Schema} Object Type", name);
                return null;
            }

            objectType.Reference = $"#/components/schemas/{name}";
            Components.Add(objectType.Reference, objectType);
            return objectType;
        }

        // allow properties to be reused as "references"
        var field = BuildField(name, schema, null);
        if (field?.Field != null)
        {
            _logger.LogInformation("Added {Field} as {Ref}", field.Field.Type, $"#/components/schemas/{name}");
            Components.Add($"#/components/schemas/{name}", field.Field);
            return field;
        }

        _logger.LogError("Couldn't build Component for {Schema}", name);
        return null;
    }

    public Schema BuildObjectType(string name, OpenApiSchema schema)
    {
        // In v2.0, if we receive OpenApiSchema (not a reference), it's already resolved
        // UnresolvedReference property no longer exists

        var writer = new ObjectWriter();
        // SerializeAsV3WithoutReference no longer exists in v2.0
        // Use the new async serialization - but since we need sync here, we'll handle this differently
        // For now, commenting out - this might need restructuring to work with the new API
        // await schema.SerializeAsV3Async(writer); // v2.0 async version

        var baseObjectType = default(string);
        var missingRef = default(MissingRef);

        switch (schema.Type)
        {
            case JsonSchemaType.Object:
            case null: // ????
                break;

            default:
            {
                _logger.LogError("Unexpected {Type} for {Schema}", schema.Type, name);
                return null;
            }
        }

        if (schema?.AllOf?.Count > 0)
        {
            if (schema.AllOf.Count == 1)
            {
                if (schema.AllOf[0] is OpenApiSchemaReference schemaRef)
                {
                    var refId = $"#/components/schemas/{schemaRef.Id}";
                    if (Components.TryGetValue(refId, out var obj) && obj is ObjectType bot)
                    {
                        baseObjectType = bot.FullName;
                        _logger.LogInformation("Use AllOf {Ref} as {BaseObjectType}", refId, baseObjectType);
                    }
                    else
                    {
                        missingRef = new MissingRef
                        {
                            Ref = refId,
                            Type = MissingRefType.BaseObjectType,
                        };

                        MissingRefs.Add(missingRef);
                    }
                }
                else
                {
                    _logger.LogError("All of that is not a reference!??!!?");
                    return null;
                }
            }
            else
            {
                _logger.LogError("AllOf: Not supported yet. Should build by combining all");
                return null;
            }
        }

        if (schema?.AnyOf?.Count > 1)
        {
            _logger.LogError("AnyOf: Not supported yet. What to do when there is no discriminator and there is no overlap");
            return null;
        }

        if (schema?.OneOf?.Count > 1)
        {
            _logger.LogError("OneOf: Not supported yet. What to do when there is no discriminator and there is no overlap");
            return null;
        }


        var createdObjectType = buildObject();

        missingRef?.Object = createdObjectType;

        if ( schema.Properties!=null) foreach (var field in schema.Properties)
        {
            var ft = BuildField(field.Key, field.Value, createdObjectType.ObjectType);
            if (ft == null)
            {
                _logger.LogError("Couldn't calculate field for {Property}: {Type}", field.Key, field.Value.Type);
                continue;
            }

            // not an exact translation but...
            // In v2.0, nullable is represented by Type including JsonSchemaType.Null
            var isNullable = schema.Type?.HasFlag(JsonSchemaType.Null) ?? false;
            ft.Field.IsRequired = (schema.Required?.Contains(field.Key) ?? false) && !isNullable;

            createdObjectType.ObjectType.Fields.Add(ft.Field.Name, ft);
        }

        return createdObjectType;

        Schema buildObject()
        {
            var nameParts = name.Split('.');
            var objectType = new Schema(
                new ObjectType()
                {
                    AccountId = AccountId,
                    EntityId = EntityId,
                    Id = Guid.NewGuid(),
                    CreatedOn = DateTime.UtcNow,
                    Name = nameParts[^1],
                    Namespace = string.Join('.', nameParts.Take(nameParts.Length - 1)),
                    Label = schema.Title,
                    Description = schema.Description,
                    BaseObjectType = baseObjectType,
                    Fields = new Dictionary<string, FieldTemplate>(),
                    RBAC = DefaultObjectTypeRbac,
                    IsEmbedded = true,
                    IsFullTextSearchable = false,
                }
            )
            {
                Raw = writer.Result,
            };

            ObjectTypes.Add(name, objectType);

            return objectType;
        }
    }

    private FormField BuildUnknownTypeField(string name, OpenApiSchema schema, ObjectType objectType)
    {
        // In v2.0, references are separate types, so if we receive OpenApiSchema here, it's not a reference
        if (schema.AllOf?.Count == 1)
        {
            return BuildField(name, schema.AllOf[0], objectType)?.Field;
        }

        _logger.LogInformation("Completely Generic field?: {Name}", name);
        return new GenericField
        {
        };
    }

    private FormField BuildUnknownTypeFieldFromReference(string name, OpenApiSchemaReference schemaRef, ObjectType objectType)
    {
        var refId = $"#/components/schemas/{schemaRef.Id}";
        _logger.LogInformation("Property using {Ref}", refId);
        if (Components.TryGetValue(refId, out var obj))
        {
            if (obj is FormField field)
            {
                return field;
            }

            _logger.LogError("Unexpected Component Type");
            return null;
        }

        _logger.LogError("Didn't find Component, add missing ref");
        // TODO: add missing ref
        // ...
        return null;
    }

    private FieldTemplate BuildField(string name, IOpenApiSchema schemaInterface, ObjectType objectType)
    {
        // In v2.0, handle both OpenApiSchema and OpenApiSchemaReference
        if (schemaInterface is OpenApiSchemaReference schemaRef)
        {
            var refField = BuildUnknownTypeFieldFromReference(name, schemaRef, objectType);
            return refField != null ? new FieldTemplate { Field = refField } : null;
        }

        if (schemaInterface is not OpenApiSchema schema)
        {
            _logger.LogError("Unexpected schema type for {Name}", name);
            return null;
        }

        var field = schema.Type switch
        {
            JsonSchemaType.Object => BuildObjectField(name, schema, objectType?.FullName),
            JsonSchemaType.String => BuildStringField(name, schema),
            JsonSchemaType.Array => BuildArrayField(name, schema, objectType?.FullName),
            JsonSchemaType.Integer => BuildIntegerField(name, schema),
            JsonSchemaType.Number => BuildNumberField(name, schema),
            JsonSchemaType.Boolean => BuildBooleanField(name, schema),
            null => BuildUnknownTypeField(name, schema, objectType),
            _ => throw new Exception($"Unexpected Type: {schema.Type}")
        };

        if (field == null) return null;

        var permissions = schema.ReadOnly ? FieldPermission.Read :
            schema.WriteOnly ? (FieldPermission.SetOnCreate | FieldPermission.Update) : (FieldPermission.SetOnCreate | FieldPermission.Read | FieldPermission.Update);

        field.Label ??= schema.Title;
        field.Name ??= name;
        field.Description ??= schema.Description;
        field.DefaultValue ??= schema.Default;
        // field.Label ??= "";

        return new FieldTemplate
        {
            RBAC = new FieldRBAC
            {
                Permissions = new Dictionary<string, FieldPermission>
                {
                    { nameof(EntityRoleId.Account), FieldPermission.Read | FieldPermission.Update | FieldPermission.SetOnCreate },
                    { nameof(EntityRoleId.Admin), permissions }
                }
            },
            Field = field,
        };
    }

    private FormField BuildBooleanField(string name, OpenApiSchema schema)
    {
        return new CheckboxField
        {
            CheckboxFieldOptions = new CheckboxFieldOptions
            {
            },
        };
    }

    private FormField BuildNumberField(string name, OpenApiSchema schema)
    {
        // possible formats
        // "number" : represents any number.
        // "number", "format": "float": 32-bit floating-point number.
        // "number", "format": "double": 64-bit floating-point number.

        return new NumberField
        {
            NumberFieldOptions = new NumberFieldOptions
            {
                Minimum = decimal.TryParse(schema.Minimum, out var min) ? min : null,
                Maximum = decimal.TryParse(schema.Maximum, out var max) ? max : null,
                MultipleOf = schema.MultipleOf,
                ExcludeMaximum = bool.TryParse(schema.ExclusiveMaximum, out var exMax) ? exMax : null,
                ExcludeMinimum = bool.TryParse(schema.ExclusiveMinimum, out var exMin) ? exMin : null
            },
        };
    }

    private FormField BuildIntegerField(string name, OpenApiSchema schema)
    {
        // possible formats
        // "integer": Represents an integer.
        // "integer", "format": "int32": 32-bit signed integer.
        // "integer", "format": "int64": 64-bit signed integer.

        return new NumberField
        {
            NumberFieldOptions = new NumberFieldOptions
            {
                DecimalPlaces = 0,

                Minimum = decimal.TryParse(schema.Minimum, out var min) ? min : null,
                Maximum = decimal.TryParse(schema.Maximum, out var max) ? max : null,
                MultipleOf = schema.MultipleOf,
                ExcludeMaximum = bool.TryParse(schema.ExclusiveMaximum, out var exMax) ? exMax : null,
                ExcludeMinimum = bool.TryParse(schema.ExclusiveMinimum, out var exMin) ? exMin : null
            },
        };
    }

    private FormField BuildArrayField(string name, OpenApiSchema schema, string parentFullName)
    {
        // TODO: ADD SUPPORT FOR IT?
        // schema.Items.UniqueItems

        if (schema.Items?.OneOf?.Count > 0)
        {
            _logger.LogError("Add support for Arrays with mixed item types (OneOf)");
            return null;
        }

        if (schema.Items?.Type == JsonSchemaType.String)
        {
            if (schema.Items.Enum?.Count > 0)
            {
                // In v2.0, Enum uses JsonNode instead of OpenApiString
                var values = schema.Items.Enum.OfType<JsonValue>()
                    .Where(x => x != null)
                    .ToDictionary(x => x.ToString(), x => x.ToString());

                if (schema.Items.Extensions?.TryGetValue("x-enumDescriptions", out var descriptions) ?? false)
                {
                    if (descriptions is JsonNodeExtension jne && jne.Node is JsonObject jo)
                    {
                        foreach (var kvp in jo)
                        {
                            if (values.ContainsKey(kvp.Key) && kvp.Value is JsonValue jv)
                            {
                                values[kvp.Key] = jv.ToString();
                            }
                        }
                    }
                }

                // it is enum => SelectField 
                // TODO: look at x-enumDescriptions for descriptions
                // ...
                return new MultiSelectField
                {
                    MultiSelectFieldOptions = new MultiSelectFieldOptions
                    {
                        Items = values,
                    }
                };
            }

            if (schema.Items is OpenApiSchema strItemsSchema)
            {
                return new ArrayField
                {
                    ArrayFieldOptions = new ArrayFieldOptions
                    {
                        ValueField = BuildStringField("value", strItemsSchema),
                    }
                };
            }
        }

        if (schema.Items?.Type == JsonSchemaType.Number && schema.Items is OpenApiSchema numItemsSchema)
        {
            return new ArrayField
            {
                ArrayFieldOptions = new ArrayFieldOptions
                {
                    ValueField = BuildNumberField("value", numItemsSchema),
                }
            };
        }

        if (schema.Items?.Type == JsonSchemaType.Integer && schema.Items is OpenApiSchema intItemsSchema)
        {
            return new ArrayField
            {
                ArrayFieldOptions = new ArrayFieldOptions
                {
                    ValueField = BuildIntegerField("value", intItemsSchema),
                }
            };
        }

        if (schema.Items?.Type == JsonSchemaType.Object)
        {
            if (schema.Items is OpenApiSchemaReference itemsRef)
            {
                return new ChildrenField
                {
                    ChildrenFieldOptions = new ChildrenFieldOptions
                    {
                        KeyType = ChildrenFieldOptions.IndexKeyType,
                        ObjectType = $"{Namespace}.{itemsRef.Id}"
                    }
                };
            }

            if (schema.Items is OpenApiSchema objItemsSchema && objItemsSchema.Properties != null)
            {
                var objectType = BuildObjectType($"{parentFullName}.{name}", objItemsSchema);
                if (objectType == null)
                {
                    _logger.LogError("Failed to create ObjectType for {Property}", name);
                    return null;
                }

                objectType.ObjectType.IsEmbedded = true;

                return new ChildrenField
                {
                    ChildrenFieldOptions = new ChildrenFieldOptions
                    {
                        KeyType = ChildrenFieldOptions.IndexKeyType,
                        ObjectType = objectType.ObjectType.FullName,
                    }
                };
            }
        }

        if (schema.Items?.Type == null)
        {
            if (schema.Items is OpenApiSchemaReference)
            {
                return new ArrayField
                {
                    ArrayFieldOptions = new ArrayFieldOptions
                    {
                        ValueField = BuildField("value", schema.Items, null)?.Field,
                    }
                };
            }

            return new GenericField
            {
            };
        }

        return null;
    }

    private FormField BuildStringField(string name, OpenApiSchema schema)
    {
        if (schema.Enum?.Count > 0)
        {
            // it is an enum
            if (schema.Enum?.Count > 0)
            {
                // In v2.0, Enum uses JsonNode instead of OpenApiString
                var values = schema.Enum.OfType<JsonValue>()
                    .Where(x => x != null)
                    .ToDictionary(x => x.ToString(), x => x.ToString());

                if (schema.Extensions?.TryGetValue("x-enumDescriptions", out var descriptions) ?? false)
                {
                    if (descriptions is JsonNodeExtension jne && jne.Node is JsonObject jo)
                    {
                        foreach (var kvp in jo)
                        {
                            if (values.ContainsKey(kvp.Key) && kvp.Value is JsonValue jv)
                            {
                                values[kvp.Key] = jv.ToString();
                            }
                        }
                    }
                }

                // it is enum => SelectField 
                // TODO: look at x-enumDescriptions for descriptions
                // ...
                return new SelectField
                {
                    SelectFieldOptions = new SelectFieldOptions
                    {
                        Items = values,
                    }
                };
            }

            return null;
        }

        // https://swagger.io/docs/specification/data-models/data-types/
        switch (schema.Format)
        {
            case "password":
                return new PasswordField
                {
                    PasswordFieldOptions = new PasswordFieldOptions
                    {
                        MaxLength = schema.MaxLength,
                        Pattern = schema.Pattern,
                    },
                };

            case "date":
                // should add support for pattern?
                // ...
                return new DateField
                {
                };

            case "date-time":
                // should add support for pattern?
                // ...
                return new DateTimeField
                {
                    //  ...
                };

            case "byte":
            case "binary":
                _logger.LogError("{Format} not supported yet", schema.Format);
                break;

            case "email":
                return new EmailField
                {
                };

            case "uri":
                return new UrlField
                {
                };

            // hostname
            // ipv4
            // ipv6
        }

        // format: [NULL], uuid, ...
        return new TextField
        {
            TextFieldOptions = new TextFieldOptions
            {
                MaxLength = schema.MaxLength,
                Format = schema.Format,
                Pattern = schema.Pattern,
            },
        };
    }

    private FormField BuildObjectField(string name, OpenApiSchema schema, string parentFullName)
    {
        // In v2.0, references are separate types (OpenApiSchemaReference)
        // If this function receives an OpenApiSchema, it's already a concrete schema, not a reference
        // The reference case is now handled in BuildField before calling this method

        // Note: schema.Reference property no longer exists in v2.0
        // Keeping logic but commenting out the old reference check
        /*
        if (schema.Reference != null)
        {
            ... old reference handling code removed ...
        }
        */

        // If we need to handle object references here, they should come through
        // a different code path now (via BuildUnknownTypeFieldFromReference)

        var objectType = BuildObjectType($"{parentFullName}.{name}", schema);
        if (objectType == null)
        {
            _logger.LogError("Failed to create ObjectType for {Property}", name);
            return null;
        }

        objectType.ObjectType.IsEmbedded = true;

        // _logger.LogInformation("ObjectField for {Schema}: {Ref}", name, objectType.FullName);
        return new ObjectField
        {
            ObjectFieldOptions = new ObjectFieldOptions
            {
                ObjectType = objectType.ObjectType.FullName,
            }
        };
    }

    public OpenApiParser ParseSchemas()
    {
        foreach (var kvp in OpenApiDocument.Components.Schemas)
        {
            if (kvp.Value is OpenApiSchema schema)
            {
                BuildSchemaComponent(kvp.Key, schema);
            }
            else if (kvp.Value is OpenApiSchemaReference schemaRef)
            {
                _logger.LogInformation("Skipping schema reference in components: {Name} -> {Ref}", kvp.Key, schemaRef.Id);
            }
        }

        return this;
    }

    public OpenApiParser ParseResponses()
    {
        foreach (var kvp in OpenApiDocument.Components.Responses)
        {
            var writer = new ObjectWriter();
            // SerializeAsV3WithoutReference no longer exists in v2.0
            // Commenting out for now - may need async refactoring
            // await kvp.Value.SerializeAsV3Async(writer);

            var contentObjectTypes = new Dictionary<string, string>();
            foreach (var c in kvp.Value.Content)
            {
                if (c.Value.Schema is OpenApiSchemaReference schemaRef)
                {
                    var name = schemaRef.Id;
                    if (ObjectTypes.TryGetValue($"{Namespace}.{name}", out var objectType))
                    {
                        _logger.LogInformation("Fond content object Type for {Response} {ContentType}: {ObjectType}", kvp.Key, c.Key, objectType.ObjectType.FullName);
                        contentObjectTypes.Add(c.Key, objectType.ObjectType.FullName);
                        continue;
                    }

                    _logger.LogError("Didn't find content object Type for {Response} {ContentType}", kvp.Key, c.Key);
                    continue;
                }

                _logger.LogError("Inline? object Type for {Response} {ContentType}", kvp.Key, c.Key);

                // var response = new OApiResponse
                // {
                //     Name = kvp.Key,
                //     Description = kvp.Value.Description,
                //     Reference = $"#/components/responses/{kvp.Key}", // TODO: handle multiple content?
                //     Raw = writer.Result,
                //     ContentObjectTypes = contentObjectTypes,
                //     // headers
                //     // extensions
                //     // links
                // };
                //
                // Responses.Add(kvp.Key, response);
                // Components.Add(response.Reference, response);
            }
        }

        return this;
    }

    public OpenApiParser ParsePaths()
    {
        foreach (var kvp in OpenApiDocument.Paths)
        {
            if (kvp.Value is not OpenApiPathItem pathItem)
            {
                _logger.LogWarning("Skipping non-OpenApiPathItem path: {Path}", kvp.Key);
                continue;
            }
            var result = Parse(kvp.Key, pathItem);
            if (result == null)
            {
                _logger.LogError("Failed to parse {Path}", kvp.Key);
                continue;
            }

            Paths.Add(result.Name, result);
        }

        return this;
    }

    public OpenApiParser ParseOperation(string path, string operationId)
    {
        if (!OpenApiDocument.Paths.TryGetValue(path, out var pathItem))
        {
            return this;
        }

        var apiOperationKvp = pathItem?.Operations.FirstOrDefault(o => o.Value.OperationId == operationId);
        if (!apiOperationKvp.HasValue)
        {
            return null;
        }

        var apiOperation = apiOperationKvp.Value.Value;

        var operation = ParseOperation(path, apiOperationKvp.Value.Key, operationId, apiOperation);

        Components.Add($"#/components/operations/{operation.OperationId}", operation);
        Operations.Add(operation.OperationId, operation);

        return this;
    }

    private Path Parse(string name, OpenApiPathItem path)
    {
        // In v2.0, path references are handled separately
        // If we receive an OpenApiPathItem, it's already a concrete path, not a reference

        var operations = new Dictionary<string, Operation>();
        foreach (var op in path.Operations)
        {
            var operation = ParseOperation(name, op.Key, name, op.Value);
            if (operation == null) return null;

            operations.Add(op.Value.OperationId, operation);
            Components.Add($"#/components/operations/{operation.OperationId}", operation);
            Operations.Add(operation.OperationId, operation);
        }

        var writer = new ObjectWriter();
        // SerializeAsV3WithoutReference no longer exists in v2.0
        // Commenting out for now
        // await path.SerializeAsV3Async(writer);

        return new Path
        {
            Name = name,
            Description = path.Description,
            Raw = writer.Result,
            Operations = operations,
        };
    }


    private Operation ParseOperation(string path, HttpMethod method, string name, OpenApiOperation op)
    {
        if (Components.ContainsKey($"#/components/operations/{op.OperationId}"))
        {
            _logger.LogInformation("There is already an operation with {OperationId}", op.OperationId);
            op.OperationId = getUniqueOperationId(op.OperationId);
        }

        var objectTypeName = $"operation.{op.OperationId}.Parameters";
        var paramOT = BuildParametersObjectType($"{Namespace}.{name}", op, objectTypeName);
        var operation = new Operation
        {
            Id = Guid.NewGuid(),
            AccountId = AccountId,
            Namespace = Namespace,
            CreatedOn = DateTime.UtcNow,
            Name = op.Summary ?? op.OperationId,
            Summary = op.Summary,
            Description = op.Description,
            OperationId = op.OperationId,
            Request = new()
            {
                Path = path,
                Method = method.Method.ToUpperInvariant(),
                ParametersObjectType = paramOT?.ObjectType.FullName,
                Parameters = paramOT?.ObjectType.Fields.Count > 0 ? paramOT.ObjectType.Fields.ToDictionary(x => x.Key, x => x.Value.Field) : null,
                ParametersPlacement = op.Parameters.ToDictionary(x => x.Name, x => x.In?.ToString())
            },
            Tags = op.Tags?.Select(x => x.Name).ToArray(),
        };

        if (op.RequestBody != null)
        {
            if (op.RequestBody.Content?.Count > 0)
            {
                var index = 0;
                foreach (var contentKvp in op.RequestBody.Content)
                {
                    if (contentKvp.Key != "application/json")
                    {
                        _logger.LogInformation("Skipping body as it is not JSON: {contentType}", contentKvp.Key);
                        continue;
                    }

                    var contentObjectTypeName = $"operation.{op.OperationId}.Body";
                    if (index > 0) contentObjectTypeName += $"_{index}";

                    var contentFullObjectTypeName = $"{Namespace}.{contentObjectTypeName}";
                    if (contentKvp.Value.Schema is OpenApiSchemaReference schemaRef)
                    {
                        if (Components.TryGetValue(schemaRef.Id, out var obj) && obj is ObjectType bot)
                        {
                            contentFullObjectTypeName = bot.FullName;
                        }
                        else
                        {
                            var missingRef = new MissingRef
                            {
                                Ref = $"#/components/schemas/{schemaRef.Id}",
                                Type = MissingRefType.BaseObjectType,
                            };

                            MissingRefs.Add(missingRef);
                        }
                    }
                    else if (contentKvp.Value.Schema is OpenApiSchema requestSchema)
                    {
                        var objectType = BuildObjectType(contentFullObjectTypeName, requestSchema);
                        if (objectType == null)
                        {
                            _logger.LogError("Failed to create {Schema} Object Type", contentObjectTypeName);
                            return null;
                        }

                        objectType.ObjectType.Description = $"{op.Summary ?? op.OperationId}: Request Body ({contentKvp.Key})";
                    }

                    operation.Request.Payloads ??= new Dictionary<string, Payload>();
                    operation.Request.Payloads.Add(contentKvp.Key, new Payload
                    {
                        ContentType = contentKvp.Key,
                        ObjectType = contentFullObjectTypeName,
                    });

                    index++;
                }
            }
        }

        foreach (var response in op.Responses)
        {
            var apiResponse = new Response
            {
                Description = response.Value.Description,
            };

            if (response.Value.Headers?.Count > 0)
            {
                // TODO: ????
                // ...
                _logger.LogInformation("Response has headers?");
            }

            var index = 0;
            foreach (var contentKvp in response.Value.Content)
            {
                var type = "Object";

                var contentObjectTypeName = $"operation.{op.OperationId}.Response.{response.Key}";
                if (index > 0) contentObjectTypeName += $"_{index}";

                var contentFullObjectTypeName = $"{Namespace}.{contentObjectTypeName}";
                if (contentKvp.Value.Schema is OpenApiSchemaReference responseSchemaRef)
                {
                    if (Components.TryGetValue(responseSchemaRef.Id, out var obj) && obj is ObjectType bot)
                    {
                        contentFullObjectTypeName = bot.FullName;
                    }
                    else
                    {
                        var missingRef = new MissingRef
                        {
                            Ref = $"#/components/schemas/{responseSchemaRef.Id}",
                            Type = MissingRefType.BaseObjectType,
                        };

                        MissingRefs.Add(missingRef);

                        // TODO: assume it is a schema ... maybe something else
                        // ...
                        contentFullObjectTypeName = $"{Namespace}.{responseSchemaRef.Id}";
                    }
                }
                else if (contentKvp.Value.Schema is OpenApiSchema responseSchema)
                {
                    if (responseSchema.Type == JsonSchemaType.Array)
                    {
                        // result is array
                        type = "Array";

                        if (responseSchema.Items?.Type == JsonSchemaType.Object)
                        {
                            // array of objects
                            if (responseSchema.Items is OpenApiSchemaReference itemsSchemaRef)
                            {
                                // LOOK for object type
                                contentFullObjectTypeName = $"{Namespace}.{itemsSchemaRef.Id}";
                            }
                            else if (responseSchema.Items is OpenApiSchema itemsSchema)
                            {
                                var objectType = BuildObjectType(contentFullObjectTypeName, itemsSchema);
                                if (objectType == null)
                                {
                                    _logger.LogError("Failed to create {Schema} Object Type", contentObjectTypeName);
                                    return null;
                                }

                                objectType.ObjectType.Description = $"{op.Summary ?? op.OperationId}: Response {response.Key} ({contentKvp.Key})";
                            }
                        }
                        else
                        {
                            _logger.LogError("array response that is not of objects");
                            return null;
                        }
                    }
                    else
                    {
                        var objectType = BuildObjectType(contentFullObjectTypeName, responseSchema);
                        if (objectType == null)
                        {
                            _logger.LogError("Failed to create {Schema} Object Type", contentObjectTypeName);
                            return null;
                        }

                        objectType.ObjectType.Description = $"{op.Summary ?? op.OperationId}: Response {response.Key} ({contentKvp.Key})";
                    }
                }

                apiResponse.Payloads ??= new Dictionary<string, Payload>();
                apiResponse.Payloads.Add(contentKvp.Key, new Payload
                {
                    ContentType = contentKvp.Key,
                    ObjectType = contentFullObjectTypeName,
                    Type = type,
                });

                index++;
            }

            operation.Responses ??= new Dictionary<string, Response>();
            operation.Responses.Add(response.Key, apiResponse);
        }

        var writer = new ObjectWriter();
        writer.WriteStartObject();
        writer.WriteOptionalObject(
            op.OperationId,
            op,
            (w, o) => o.SerializeAsV3(w));
        writer.WriteEndObject();

        operation.Raw = writer.Result;

        return operation;

        string getUniqueOperationId(string operationId)
        {
            for (var c = 1; c < 10; c++)
            {
                var candidate = $"{operationId}_{c}";
                if (!Components.ContainsKey($"#/components/operations/{candidate}")) return candidate;
            }

            throw new Exception($"Couldn't generate unique operation id for {operationId}");
        }
    }

    private Schema BuildParametersObjectType(string pathName, OpenApiOperation operation, string objectTypeName)
    {
        var nameParts = $"{Namespace}.{objectTypeName}".Split('.');
        var objetType = new Schema(
            new ObjectType
            {
                AccountId = AccountId,
                EntityId = EntityId,
                Id = Guid.NewGuid(),
                CreatedOn = DateTime.UtcNow,
                Name = nameParts[^1],
                Namespace = string.Join('.', nameParts.Take(nameParts.Length - 1)),
                Description = $"{operation.Summary ?? operation.OperationId}: Parameters",
                Fields = new(),
                RBAC = DefaultObjectTypeRbac,
                IsEmbedded = true,
                IsFullTextSearchable = false,
            }
        );

        foreach (var p in operation.Parameters)
        {
            var field = BuildField(p.Name, p.Schema, objetType.ObjectType);
            if (field == null)
            {
                _logger.LogError("Couldn't build Field for {OperationId}.{Parameter} @ {Path}", operation.OperationId, p.Name, pathName);
                continue;
            }

            objetType.ObjectType.Fields.Add(p.Name, field);
        }

        ObjectTypes.Add(objectTypeName, objetType);

        return objetType;
    }

    public Task<OpenApiParser> LoadLocalFile(string filepath)
    {
        var stream = File.Open(filepath, FileMode.Open);
        return LoadAsync(stream);
    }

    public async Task<OpenApiParser> LoadAsync(Stream stream)
    {
        var result = await OpenApiDocument.LoadAsync(stream);
        OpenApiDocument = result.Document;
        Diagnostic = result.Diagnostic;
        return this;
    }

    public OpenApiParser ResolveMissingLinks()
    {
        foreach (var link in MissingRefs)
        {
            if (!Components.TryGetValue(link.Ref, out var refObj))
            {
                _logger.LogError("Failed to resolve {MissingRef}", link.Ref);
                continue;
            }

            switch (link.Type)
            {
                case MissingRefType.BaseObjectType:
                {
                    var objectType = (link.Object as Schema)?.ObjectType;
                    var refObjectType = (refObj as Schema)?.ObjectType;
                    if (objectType == null || refObjectType == null)
                    {
                        continue;
                        // throw new Exception("Invalid Link");
                    }

                    objectType.BaseObjectType = refObjectType.FullName;
                    break;
                }

                case MissingRefType.ObjectField:
                {
                    _logger.LogInformation("Missing Object for {ReferenceId}", link.Ref);
                    break;
                }
            }
        }

        return this;
    }
}

public enum MissingRefType
{
    BaseObjectType,
    ObjectField
}

public class MissingRef
{
    public string Ref { get; set; }
    public object Object { get; set; }
    public MissingRefType Type { get; set; }
}