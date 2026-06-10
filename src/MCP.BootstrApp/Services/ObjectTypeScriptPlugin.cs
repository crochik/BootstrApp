using System.Collections;
using Crochik.Mongo;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;
using ScriptInterpreter;
using ScriptInterpreter.Execution;
using ScriptInterpreter.Plugins;
using ScriptInterpreter.Registry;
using ScriptInterpreter.TypeSystem;
using ObjectType = PI.Shared.Models.ObjectType;

namespace MCP.Services;

public class ObjectTypeScriptPlugin : IScriptPlugin, IMethodRegistry, ITypeRegistry, IOperationInvoker
{
    public IEntityContext EntityContext { get; set; }
    public readonly HashSet<string> Namespaces = [];

    private readonly ILogger<ObjectTypeScriptPlugin> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly IServiceProvider _serviceProvider;

    private readonly Dictionary<string, EndpointMethod> _methods = new();
    private readonly Dictionary<string, TypeMetadata> _types = new();
    private readonly Dictionary<string, ObjectType> _objectTypes = new();

    public IReadOnlyCollection<string> OwnedNamespaces => Namespaces;
    public IMethodRegistry Methods => this;
    public ITypeRegistry Types => this;
    public IOperationInvoker? Invoker => this;
    public PropertyNameResolver? NameResolver { get; } = new PropertyNameResolver(InterpreterOptions.Default.DefaultPropertyNaming);

    // private readonly Dictionary<string, EndpointMethod> _unqualifiedMethods = new();
    // private readonly Dictionary<string, TypeMetadata> _unqualifiedTypes = new();

    public ObjectTypeScriptPlugin(ILogger<ObjectTypeScriptPlugin> logger, MongoConnection connection, ObjectTypeService objectTypeService, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _serviceProvider = serviceProvider;
    }

    // public async Task<Result<string>> GenerateScriptAsync(IEntityContext context, string namespaceRegex)
    // {
    //     await LoadObjectTypesAsync(context, namespaceRegex);
    //
    //     var dts = new TypeScriptDeclarationGenerator(this).Generate();
    //     return Result.Success(dts);
    // }

    public async Task LoadObjectTypesAsync(IEntityContext context, string namespaceRegex)
    {
        EntityContext = context;

        var objectTypes = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, context.AccountId)
            .Regex(x => x.Namespace, namespaceRegex)
            // .BitsAnySet(x => x.RBAC.Permissions[context.ProfileId.ToString()], 0b111 /*CRU*/)
            .IncludeFields(x => x.Name, x => x.Namespace, x => x.Id, x => x.RBAC, x => x.FullName)
            .FindAsync();

        foreach (var ot in objectTypes)
        {
            var objectType = await _objectTypeService.GetAsync(context, ot.FullName);
            Add(objectType);
        }

        await LoadUserActionsAsync(context, objectTypes);
    }

    private async Task LoadUserActionsAsync(IEntityContext context, List<ObjectType> objectTypes)
    {
        // TODO: add more constraints, no need to get a lot of them 
        // - context access
        // - user trigger 
        // ....
        var userActions = await _connection.Filter<EventType>()
            .Eq(x => x.AccountId, context.AccountId)
            .In(x => x.ObjectType, objectTypes.Select(x => x.FullName))
            .FindAsync();

        foreach (var ot in userActions)
        {
            Add(ot);
        }
    }

    private void Add(ObjectType objectType)
    {
        if (objectType.Namespace != null) Namespaces.Add(objectType.Namespace);

        if (!objectType.IsEmbedded && !objectType.IsAbstract)
        {
            // register methods 
            if (objectType.CanRead(EntityContext)) Add(new EndpointMethod { ObjectType = objectType, Endpoint = ActionEndpoint.Filter });
            // if (objectType.CanRead(context)) Add(new EndpointMethod { ObjectType = objectType, Endpoint = ActionEndpoint.Get });
            if (objectType.CanCreate(EntityContext)) Add(new EndpointMethod { ObjectType = objectType, Endpoint = ActionEndpoint.Create });
            if (objectType.CanUpdate(EntityContext)) Add(new EndpointMethod { ObjectType = objectType, Endpoint = ActionEndpoint.Update });
            if (objectType.CanDelete(EntityContext)) Add(new EndpointMethod { ObjectType = objectType, Endpoint = ActionEndpoint.Delete });
        }

        // register types
        AddType(objectType);

        _objectTypes.Add(objectType.FullName, objectType);
    }

    private void Add(EventType eventType)
    {
        if (eventType.Trigger is not UserTrigger trigger) return;

        if (!_objectTypes.TryGetValue(eventType.ObjectType, out var objectType))
        {
            // TODO: error
            return;
        }

        if (trigger.InputObjectType != null || trigger.OutputObjectType != null)
        {
            var inputObject = default(ObjectType?);
            var outputObject = default(ObjectType?);
            if (trigger.InputObjectType != null && !_objectTypes.TryGetValue(trigger.InputObjectType, out inputObject))
            {
                // TODO: error
                return;
            }

            if (trigger.OutputObjectType != null && !_objectTypes.TryGetValue(trigger.OutputObjectType, out outputObject))
            {
                // TODO: error
                return;
            }

            var methodName = $"{trigger.Name ?? eventType.Name}";
            if (_methods.ContainsKey(methodName))
            {
                methodName = $"exec{objectType.ApiName ?? objectType.Name}{trigger.Name ?? eventType.Name}";
            }

            var endpointMethod = new EndpointMethod
            {
                ObjectType = objectType,
                MethodMetadata = new MethodMetadata(
                    methodName,
                    inputObject.Namespace,
                    [
                        new MethodParameterMetadata("args", new NamedTypeReference(inputObject.ApiName ?? inputObject.Name), true, Description: inputObject.Description)
                    ],
                    new NamedTypeReference(outputObject.ApiName ?? outputObject.Name),
                    true,
                    Description: eventType.Description
                ),
            };

            var qualifiedMethod = inputObject.Namespace != null ? $"{inputObject.Namespace}.{methodName}" : methodName;
            _methods.Add(qualifiedMethod, endpointMethod);
            // _unqualifiedMethods.Add(methodName, endpointMethod);
        }
    }

    private string GetApiName(ObjectType objectType)
    {
        if (objectType.ApiName != null) return objectType.Namespace != null ? $"{objectType.Namespace}.{objectType.ApiName}" : objectType.ApiName;
        return objectType.FullName;
    }

    private void AddType(ObjectType objectType)
    {
        var metaType = BuildTypeMetadata(objectType, null);

        _types.Add(GetApiName(objectType), metaType);
    }

    private ScriptInterpreter.TypeSystem.ObjectType BuildObjectTypeMetadata(ObjectType objectType, ActionEndpoint? endpoint)
    {
        var props = new Dictionary<string, PropertyType>(fields());

        var req = endpoint switch
        {
            ActionEndpoint.Update or ActionEndpoint.Filter => [Model.IdFieldName],
            ActionEndpoint.Create or null => objectType.Fields
                .Where(x => x.Value.Field.IsRequired)
                .Select(x => x.Value.Field.ApiName ?? x.Value.Field.Name)
                .Where(props.ContainsKey)
                .ToHashSet(),

            _ => [],
        };

        return new ScriptInterpreter.TypeSystem.ObjectType(
            Properties: props,
            Required: req,
            Description: objectType.Description);

        IEnumerable<KeyValuePair<string, PropertyType>> fields()
        {
            foreach (var kvp in objectType.Fields)
            {
                if (kvp.Value.Field switch
                    {
                        HiddenField _ => true,
                        CalculatedField _ => true,
                        RelatedObjectsField _ => true,
                        LabelField _ => true,
                        null => true,
                        _ => false, // kvp.Value.InitialValue != null,
                    }) continue;

                // skip the ones that are set on the server
                if (endpoint == ActionEndpoint.Create && (kvp.Value.InitialValue != null || !kvp.Value.RBAC.CanSetOnCreate(EntityContext)))
                {
                    continue;
                }

                if (endpoint == ActionEndpoint.Update && (kvp.Value.CalculatedValue != null || !kvp.Value.RBAC.CanUpdate(EntityContext))) continue;
                if (endpoint == ActionEndpoint.Filter && !kvp.Value.RBAC.CanRead(EntityContext)) continue;

                var field = GetTypeInfo(kvp.Value.Field);
                if (field == null) continue;

                var description = kvp.Value.Field.Description ?? kvp.Value.Field.Label ?? kvp.Value.Field.Name;
                if (kvp.Value.Field.DefaultValue != null) description += $"\n@default {kvp.Value.Field.DefaultValue}";
                yield return new KeyValuePair<string, PropertyType>(kvp.Value.Field.ApiName ?? kvp.Key, new PropertyType(field, description));
            }
        }
    }

    private TypeMetadata BuildTypeMetadata(ObjectType objectType, ActionEndpoint? endpoint)
    {
        var metaType = new TypeMetadata(
            Name: objectType.ApiName ?? objectType.Name,
            Namespace: objectType.Namespace,
            Description: objectType.Description,
            Type: BuildObjectTypeMetadata(objectType, endpoint)
        );

        return metaType;
    }

    private TypeInfo? GetTypeInfo(FormField field)
    {
        return field switch
        {
            ArrayField a => new ArrayType(GetTypeInfo(a.ArrayFieldOptions.ValueField)!),
            BitwiseFlagField _ => PrimitiveType.Number,
            // CalculatedField _ =>
            CheckboxField _ => PrimitiveType.Boolean,
            ChildrenField c => c.ChildrenFieldOptions.KeyType == ChildrenFieldOptions.IndexKeyType ? new ArrayType(new NamedTypeReference(c.ChildrenFieldOptions.ObjectType)) : new NamedTypeReference(c.ChildrenFieldOptions.ObjectType),
            DateField _ => PrimitiveType.String,
            DateRangeField _ => new ArrayType(PrimitiveType.String),
            DateTimeField _ => PrimitiveType.String,
            // DictionaryField _ =>
            EmailField _ => PrimitiveType.String,
            ExpressionField e => GetTypeInfo(e.ExpressionFieldOptions.ValueField),
            // FileField _ =>
            GenericField _ => AnyType.Instance,
            // HiddenField _ =>
            ImageField _ => PrimitiveType.String,
            // LabelField _ =>
            // LocationDistanceField _ =>
            LocationField _ => new ArrayType(PrimitiveType.Number),
            // LookupField _ =>
            MultiReferenceField _ => new ArrayType(PrimitiveType.String),
            MultiSelectField s => new ArrayType(new EnumType(PrimitiveType.String, GetKeys(s.MultiSelectFieldOptions.Items))),
            NumberField _ => PrimitiveType.Number,
            ObjectField o => new NamedTypeReference(o.ObjectFieldOptions.ObjectType),
            PasswordField _ => PrimitiveType.String,
            PhoneField _ => PrimitiveType.String,
            PostalCodeField _ => PrimitiveType.String,
            ReferenceField _ => PrimitiveType.String,
            // RelatedObjectsField _ =>
            SelectField s => new EnumType(PrimitiveType.String, GetKeys(s.SelectFieldOptions.Items)),
            TagsField _ => new ArrayType(PrimitiveType.String),
            TextField _ => PrimitiveType.String,
            TimeField _ => PrimitiveType.String,
            UrlField _ => PrimitiveType.String,

            _ => default(TypeInfo?),
        };
    }

    private List<string> GetKeys(IDictionary items)
    {
        return keys().ToList();

        IEnumerable<string> keys()
        {
            foreach (var item in items.Keys)
            {
                if (item is string str) yield return str;
            }
        }
    }

    public IEnumerable<string> GetRegisteredNamespaces() => Namespaces;

    public async ValueTask<MethodMetadata?> GetQualifiedMethodAsync(string? namespaceName, string methodName, CancellationToken cancellationToken = default)
    {
        var fullName = namespaceName != null ? $"{namespaceName}.{methodName}" : methodName;
        if (_methods.TryGetValue(fullName, out var method))
        {
            return method.MethodMetadata;
        }

        ActionEndpoint endpoint;
        string objectTypeName;
        if (methodName.StartsWith("filter"))
        {
            endpoint = ActionEndpoint.Filter;
            objectTypeName = methodName["filter".Length..];
        }
        else if (methodName.StartsWith("delete"))
        {
            endpoint = ActionEndpoint.Delete;
            objectTypeName = methodName["delete".Length..^"ById".Length];
        }
        else if (methodName.StartsWith("create"))
        {
            endpoint = ActionEndpoint.Create;
            objectTypeName = methodName["create".Length..];
        }
        else if (methodName.StartsWith("update"))
        {
            endpoint = ActionEndpoint.Update;
            objectTypeName = methodName["update".Length..^"ById".Length];
        }
        else
        {
            _logger.LogError("{Method} not found", fullName);
            return null;
        }
        
        if (namespaceName != null) objectTypeName = $"{namespaceName}.{objectTypeName}";
        var objectType = await _objectTypeService.GetAsync(EntityContext, objectTypeName);
        if (objectType == null)
        {
            _logger.LogError("Couldn't find {ObjectType} for  {Method}", objectTypeName, methodName);
            return null;
        }
        
        Add(objectType);

        if (_methods.TryGetValue(fullName, out method))
        {
            _logger.LogInformation("Added {Method} for {ObjectType}", methodName, objectTypeName);
            return method.MethodMetadata;
        }

        _logger.LogError("{Method} not found", fullName);
        return null;
    }

    public bool HasNamespace(string namespaceName)
    {
        var result = Namespaces.Contains(namespaceName);
        return result;
    }

    private void Add(EndpointMethod method)
    {
        method.MethodMetadata = BuildMethodMetadata(method);
        var qualifiedName = method.ObjectType.Namespace != null ? $"{method.ObjectType.Namespace}.{method.MethodMetadata.Name}" : method.MethodMetadata.Name;

        _methods.Add(qualifiedName, method);
        // _unqualifiedMethods.Add(method.MethodMetadata.Name, method);
    }

    public IEnumerable<MethodMetadata> GetAllMethods()
    {
        // return _unqualifiedMethods.Values.Select(x => x.MethodMetadata!);
        return _methods.Values.Select(x => x.MethodMetadata).Where(x => x != null).Select(x => x!);
    }

    public MethodMetadata? GetQualifiedMethod(string? namespaceName, string methodName)
        => _methods.TryGetValue(namespaceName != null ? $"{namespaceName}.{methodName}" : methodName, out var method) ? method.MethodMetadata : null;

    public IEnumerable<TypeMetadata> GetAllTypes() => _types.Values;

    public TypeMetadata? GetQualifiedType(string? namespaceName, string typeName) => _types.TryGetValue(namespaceName != null ? $"{namespaceName}.{typeName}" : typeName, out var type) ? type : null;

    public async Task<object?> InvokeAsync(OperationInvocationContext context, CancellationToken ct = default)
    {
        var fullName = $"{context.Namespace}.{context.MethodName}";
        if (!_methods.TryGetValue(fullName, out var method))
        {
            throw new InvalidOperationException($"Could not find {fullName}");
        }

        await Task.CompletedTask;

        switch (method.Endpoint)
        {
            case ActionEndpoint.Filter:
                return await FilterAsync(context, method, ct);

            case ActionEndpoint.Create:
                return await CreateAsync(context, method, ct);

            default:
                break;
        }

        return null;
    }

    private async Task<object?> CreateAsync(OperationInvocationContext context, EndpointMethod method, CancellationToken ct)
    {
        var arg = context.Parameters[0].Value;
        if (arg is not IDictionary<string, object?> dict) throw new Exception($"Unexpected argument type: {arg?.GetType().Name}");

        var addOptions = new ObjectTypeService.AddObjectOptions
        {
            UseFieldApiNames = true,
        };

        var result = await _objectTypeService.AddObjectAsync(EntityContext, method.ObjectType, dict, addOptions);
        if (!result) throw new Exception(result.Status);

        return result.Value.ObjectId;
    }

    private async Task<object?> FilterAsync(OperationInvocationContext context, EndpointMethod method, CancellationToken ct)
    {
        var conditions = new List<Condition>();
        var fields = new List<string>();
        var orderBy = default(string?);
        var reverseOrder = false;
        var top = default(int?);
        var skip = default(int?);
        for (var i = 0; i < method.MethodMetadata!.Parameters.Count; i++)
        {
            var arg = context.Parameters[i].Value;

            switch (i)
            {
                case 0: // filter 
                {
                    if (arg is not IEnumerable<object?> en) throw new Exception($"filter argument is not array: {arg?.GetType().Name}");
                    foreach (var c in en)
                    {
                        if (c is not IDictionary<string, object?> dict) throw new Exception($"Unexpected condition type: {c?.GetType().Name}");
                        if (!dict.TryGetValue("field", out var field) || field is not string fieldName) throw new Exception($"Invalid or missing field in condition");
                        if (!dict.TryGetValue("operator", out var op) || op is not string opStr) throw new Exception($"Invalid missing operator in condition");
                        if (!dict.TryGetValue("value", out var value)) value = null;
                        conditions.Add(new Condition
                        {
                            FieldName = fieldName,
                            Operator = Enum.Parse<Operator>(opStr, true),
                            Value = value,
                        });
                    }

                    break;
                }

                case 1: // fields 
                {
                    if (arg is not IEnumerable<object?> en) throw new Exception($"fields argument is not array: {arg?.GetType().Name}");
                    foreach (var c in en)
                    {
                        if (c is not string str) throw new Exception($"Unexpected field type: {c?.GetType().Name}");
                        fields.Add(str);
                    }

                    break;
                }

                case 2: // order by
                {
                    orderBy = arg switch
                    {
                        null => null,
                        string str => str,
                        _ => throw new Exception($"Unexpected type for orderBy argument: {arg?.GetType().Name}"),
                    };
                    break;
                }

                case 3: // reverseOrder
                {
                    reverseOrder = arg switch
                    {
                        null => false,
                        bool bit => bit,
                        string str => bool.TryParse(str, out var bit) ? bit : throw new Exception($"Unexpected type for reverse argument: {arg?.GetType().Name}"),
                        _ => throw new Exception($"Unexpected type for reverse argument: {arg?.GetType().Name}"),
                    };
                    break;
                }

                case 4: // top
                {
                    top = arg switch
                    {
                        null => null,
                        int num => num,
                        string str => int.TryParse(str, out var num) ? num : throw new Exception($"Unexpected type for top argument: {arg?.GetType().Name}"),
                        _ => throw new Exception($"Unexpected type for top argument: {arg?.GetType().Name}"),
                    };
                    break;
                }

                case 5: // skip
                {
                    skip = arg switch
                    {
                        null => null,
                        int num => num,
                        string str => int.TryParse(str, out var num) ? num : throw new Exception($"Unexpected type for skip argument: {arg?.GetType().Name}"),
                        _ => throw new Exception($"Unexpected type for skip argument: {arg?.GetType().Name}"),
                    };
                    break;
                }
            }
        }

        var request = new DataViewRequest
        {
            Criteria = conditions.ToArray(),
            Fields = fields.ToArray(),
            OrderBy = reverseOrder ? $"-{orderBy}" : orderBy,
            Top = top ?? 0,
            Skip = skip ?? 0,
        };

        var builder = _serviceProvider.GetRequiredService<ObjectDataViewBuilder>();
        builder.UseApiNames = true;
        builder.IncludeHiddenFields = false;
        builder.AutoGenerateReferenceFieldNames = false;
        builder.SkipCustomizations = true;

        var result = await builder.BuildResultSetAsync(EntityContext, method.ObjectType, request);

        return result.ToArray();
    }

    private MethodMetadata BuildMethodMetadata(EndpointMethod method)
    {
        var objectType = method.ObjectType;
        var endpoint = method.Endpoint;
        var objectTypeName = objectType.ApiName ?? objectType.Name;
        return new
            MethodMetadata
            (
                Name: getFullName(),
                Namespace: objectType.Namespace,
                Description: endpoint switch
                {
                    ActionEndpoint.Filter => $"Return {objectTypeName} Objects that match all conditions. Return only requested fields.",
                    ActionEndpoint.Delete => $"Delete {objectTypeName} by id",
                    ActionEndpoint.Create => $"Create {objectTypeName}",
                    ActionEndpoint.Update => $"Patch {objectTypeName}. Only update fields with non-null values",
                    _ => throw new NotImplementedException($"Invalid Endpoint: {endpoint}"),
                },
                Parameters: getParameters().ToList(),
                Invocation: null, // ???
                IsAsync: true,
                Metadata: null,
                ReturnType: getReturnType()
            );

        string getFullName()
        {
            var prefix = endpoint switch
            {
                ActionEndpoint.Filter => "filter",
                ActionEndpoint.Delete => "delete",
                ActionEndpoint.Create => "create",
                ActionEndpoint.Update => "update",
                _ => throw new NotImplementedException($"Invalid Endpoint: {endpoint}"),
            };

            var suffix = endpoint switch
            {
                ActionEndpoint.Filter => "",
                ActionEndpoint.Delete => "ById",
                ActionEndpoint.Create => "",
                ActionEndpoint.Update => "ById",
                _ => throw new NotImplementedException($"Invalid Endpoint: {endpoint}"),
            };

            return $"{prefix}{objectTypeName}{suffix}";
        }

        IEnumerable<MethodParameterMetadata> getParameters()
        {
            switch (endpoint)
            {
                case ActionEndpoint.Delete:
                    yield return new MethodParameterMetadata("id", PrimitiveType.String, true, Description: $"id of {objectTypeName} to delete");
                    yield break;

                case ActionEndpoint.Update:
                {
                    var unqualifiedName = $"Update{objectType.ApiName ?? objectType.Name}Input";
                    var qualifiedName = objectType.Namespace != null ? $"{objectType.Namespace}.{unqualifiedName}" : unqualifiedName;
                    var metaType = new TypeMetadata(
                        Name: unqualifiedName,
                        Namespace: objectType.Namespace,
                        Description: objectType.Description,
                        Type: BuildObjectTypeMetadata(objectType, endpoint)
                    );

                    _types.Add(qualifiedName, metaType);
                    yield return new MethodParameterMetadata("args", new NamedTypeReference(unqualifiedName), true, Description: $"{objectType.Name} fields to be modified.");

                    // yield return new MethodParameterMetadata("args", item, true, Description: $"{objectType.Name} to be modified.");

                    // foreach (var kvp in item.Properties)
                    // {
                    //     yield return new MethodParameterMetadata(kvp.Key, kvp.Value.Type, item.Required.Contains(kvp.Key), kvp.Value.Description);
                    // }

                    yield break;
                }
                case ActionEndpoint.Create:
                {
                    var unqualifiedName = $"Create{objectType.ApiName ?? objectType.Name}Input";
                    var qualifiedName = objectType.Namespace != null ? $"{objectType.Namespace}.{unqualifiedName}" : unqualifiedName;
                    var metaType = new TypeMetadata(
                        Name: unqualifiedName,
                        Namespace: objectType.Namespace,
                        Description: objectType.Description,
                        Type: BuildObjectTypeMetadata(objectType, endpoint)
                    );

                    _types.Add(qualifiedName, metaType);
                    yield return new MethodParameterMetadata("args", new NamedTypeReference(unqualifiedName), true, Description: $"field values for the {objectType.Name} to be created.");

                    // var item = BuildObjectTypeMetadata(objectType, endpoint);
                    // yield return new MethodParameterMetadata("args", item, true, Description: $"{objectType.Name} to be added."); //new NamedTypeReference(objectType.Name)

                    // foreach (var kvp in item.Properties)
                    // {
                    //     yield return new MethodParameterMetadata(kvp.Key, kvp.Value.Type, item.Required.Contains(kvp.Key), kvp.Value.Description);
                    // }

                    yield break;
                }

                case ActionEndpoint.Filter:
                {
                    var fields = objectType.Fields
                        .Where(x => x.Value.RBAC.CanRead(EntityContext))
                        .Select(x => x.Value)
                        .ToArray();

                    var allFields = new EnumType(PrimitiveType.String, fields.Select(x => x.Field.ApiName ?? x.Field.Name).ToList());
                    var indexedFields = new EnumType(PrimitiveType.String, fields.Where(x => x.Indexed).Select(x => x.Field.ApiName ?? x.Field.Name).ToList());
                    var condition = new ScriptInterpreter.TypeSystem.ObjectType(
                        new Dictionary<string, PropertyType>
                        {
                            { "field", new PropertyType(indexedFields, "field") },
                            { "operator", new PropertyType(new EnumType(PrimitiveType.String, ["Eq", "Gt", "Gte", "In", "Lt", "Lte", "Ne", "Nin"]), "operator") },
                            { "value", new PropertyType(AnyType.Instance, "value to be used in comparison condition") },
                        },
                        [
                            "field",
                            "operator",
                        ],
                        "Conditions that an object has to match to be returned"
                    );
                    yield return new MethodParameterMetadata("filter", new ArrayType(condition), true, Description: "List of AND conditions that an object has to match to be returned");
                    yield return new MethodParameterMetadata("fields", new ArrayType(allFields), true, Description: $"Fields to return");
                    yield return new MethodParameterMetadata("orderBy", indexedFields, false, Description: $"Fields to order results by");
                    yield return new MethodParameterMetadata("reverseOrder", PrimitiveType.Boolean, false, Description: $"Whether to order results in reverse order or not.");
                    yield return new MethodParameterMetadata("top", PrimitiveType.Number, false, Description: $"Maximum number of results to return");
                    yield return new MethodParameterMetadata("skip", PrimitiveType.Number, false, Description: $"Number of results to skip before returning remaining");
                    // public Condition[] Criteria { get; set; }
                    yield break;
                }
            }
        }

        ScriptInterpreter.TypeSystem.TypeInfo getReturnType() => endpoint switch
        {
            ActionEndpoint.Filter => new ArrayType(BuildObjectTypeMetadata(objectType, endpoint)),
            ActionEndpoint.Delete => PrimitiveType.Boolean,
            ActionEndpoint.Create => PrimitiveType.String,
            ActionEndpoint.Update => PrimitiveType.Boolean,
            _ => throw new NotImplementedException($"Invalid Endpoint: {endpoint}"),
        };
    }
}

public class EndpointMethod
{
    public required ObjectType ObjectType { get; init; }
    public ActionEndpoint? Endpoint { get; init; }

    public MethodMetadata? MethodMetadata { get; set; }
}