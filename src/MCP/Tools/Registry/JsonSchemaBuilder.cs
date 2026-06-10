using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using McpServer.Models;

namespace McpServer.Tools.Registry;

/// <summary>
/// Reflection-based JSON Schema builder used for both <c>inputSchema</c> (parameters)
/// and <c>outputSchema</c> (return types) of MCP tools. Walks public properties
/// recursively, honors <see cref="JsonPropertyNameAttribute"/>, derives
/// <c>required</c> from non-nullable types, and breaks cycles via a visited set.
/// </summary>
internal static class JsonSchemaBuilder
{
    public static ToolInputSchema BuildSchemaFromParameters(IEnumerable<ToolParameterInfo> parameters)
    {
        var properties = new Dictionary<string, PropertySchema>();
        var required = new List<string>();

        foreach (var p in parameters)
        {
            var schema = BuildPropertySchema(p.ParameterType, isNullable: false, new HashSet<Type>());
            ApplyParameterDescription(schema, p.Description);
            properties[p.ParameterName] = schema;
            if (p.IsRequired)
                required.Add(p.ParameterName);
        }

        return new ToolInputSchema { Properties = properties, Required = required };
    }

    public static ToolInputSchema BuildSchemaFromType(Type returnType)
    {
        if (returnType == typeof(void) || returnType == typeof(Task))
            throw new InvalidOperationException(
                "StructuredOutput tools must return a value (or Task<T>).");

        var unwrapped = Nullable.GetUnderlyingType(returnType) ?? returnType;
        if (unwrapped == typeof(string))
            throw new InvalidOperationException(
                "StructuredOutput tools cannot return string — structuredContent requires an object payload.");

        if (unwrapped.FullName == "McpServer.Models.ToolCallResult")
            throw new InvalidOperationException(
                "StructuredOutput tools cannot return ToolCallResult directly — return a POCO whose shape can be introspected.");

        var schema = BuildPropertySchema(unwrapped, isNullable: false, new HashSet<Type>());

        // A registered override wins over reflection: emit the raw schema verbatim
        // at the top level instead of trying to project it into the typed shape.
        if (schema.RawSchema is JsonNode raw)
            return new ToolInputSchema { RawSchema = raw };

        if (schema.Type != "object")
            throw new InvalidOperationException(
                $"StructuredOutput return type must be an object (got {schema.Type} for {unwrapped.Name}).");

        return new ToolInputSchema
        {
            Type = "object",
            Properties = schema.Properties ?? new Dictionary<string, PropertySchema>(),
            Required = schema.Required ?? new List<string>()
        };
    }

    /// <summary>
    /// Apply the per-parameter <c>[McpParameter]</c> description to the schema.
    /// For raw-overridden schemas, only inject when the override didn't already
    /// specify a description — the per-type override is more specific and wins.
    /// </summary>
    private static void ApplyParameterDescription(PropertySchema schema, string description)
    {
        if (schema.RawSchema is JsonObject obj)
        {
            if (!string.IsNullOrEmpty(description) && !obj.ContainsKey("description"))
                obj["description"] = description;
            return;
        }

        schema.Description = description;
    }

    private static PropertySchema BuildPropertySchema(Type type, bool isNullable, HashSet<Type> visited)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        // A globally-registered explicit schema short-circuits reflection. Applies
        // both to top-level parameter/return types and to nested property types.
        if (McpSchemaOverrides.GetClonedNode(underlying) is JsonNode overrideNode)
            return new PropertySchema { RawSchema = overrideNode };

        if (underlying == typeof(string))
            return new PropertySchema { Type = "string" };
        if (underlying == typeof(bool))
            return new PropertySchema { Type = "boolean" };
        if (IsNumeric(underlying))
            return new PropertySchema { Type = "number" };
        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
            return new PropertySchema { Type = "string", Format = "date-time" };
        if (underlying == typeof(Guid))
            return new PropertySchema { Type = "string", Format = "uuid" };
        if (underlying.IsEnum)
            return new PropertySchema
            {
                Type = "string",
                Enum = Enum.GetNames(underlying).ToList()
            };

        if (TryGetEnumerableElementType(underlying, out var elementType))
        {
            return new PropertySchema
            {
                Type = "array",
                Items = BuildPropertySchema(elementType!, isNullable: false, visited)
            };
        }

        if (IsStringKeyedDictionary(underlying))
            return new PropertySchema { Type = "object" };

        // Cycle guard for complex object graphs.
        if (!visited.Add(underlying))
            return new PropertySchema { Type = "object" };

        try
        {
            var props = new Dictionary<string, PropertySchema>();
            var requiredNames = new List<string>();
            var ctx = new NullabilityInfoContext();

            foreach (var prop in underlying.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                var indexParams = prop.GetIndexParameters();
                if (indexParams.Length > 0) continue; // skip indexers
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;

                var name = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                           ?? ToCamelCase(prop.Name);

                bool propIsNullable;
                try
                {
                    var info = ctx.Create(prop);
                    propIsNullable = info.ReadState == NullabilityState.Nullable
                                     || Nullable.GetUnderlyingType(prop.PropertyType) != null;
                }
                catch
                {
                    propIsNullable = !prop.PropertyType.IsValueType
                                     || Nullable.GetUnderlyingType(prop.PropertyType) != null;
                }

                props[name] = BuildPropertySchema(prop.PropertyType, propIsNullable, visited);

                if (!propIsNullable)
                    requiredNames.Add(name);
            }

            return new PropertySchema
            {
                Type = "object",
                Properties = props.Count > 0 ? props : null,
                Required = requiredNames.Count > 0 ? requiredNames : null
            };
        }
        finally
        {
            visited.Remove(underlying);
        }
    }

    private static bool IsNumeric(Type t) =>
        t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
        || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(sbyte)
        || t == typeof(double) || t == typeof(float) || t == typeof(decimal);

    private static bool TryGetEnumerableElementType(Type type, out Type? elementType)
    {
        if (type == typeof(string))
        {
            elementType = null;
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return elementType != null;
        }

        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(List<>) || def == typeof(IList<>) || def == typeof(IEnumerable<>)
                || def == typeof(IReadOnlyList<>) || def == typeof(IReadOnlyCollection<>)
                || def == typeof(ICollection<>) || def == typeof(HashSet<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        var iface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType
                                 && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (iface != null)
        {
            elementType = iface.GetGenericArguments()[0];
            return true;
        }

        elementType = null;
        return false;
    }

    private static bool IsStringKeyedDictionary(Type type)
    {
        if (!type.IsGenericType) return false;
        var def = type.GetGenericTypeDefinition();
        if (def != typeof(Dictionary<,>) && def != typeof(IDictionary<,>)
            && def != typeof(IReadOnlyDictionary<,>))
            return false;
        return type.GetGenericArguments()[0] == typeof(string);
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0])) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
