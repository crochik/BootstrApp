using System.Reflection;
using System.Text.RegularExpressions;
using McpServer.Models;
using McpServer.Tools.Attributes;
using PI.Shared.Models;

namespace McpServer.Tools.Registry;

/// <summary>
/// Pre-computed description of a single discovered MCP tool method.
/// </summary>
internal sealed class ToolRegistration
{
    public required string Name { get; init; }
    public required ToolMetadata Metadata { get; init; }
    public required Type DeclaringType { get; init; }
    public required MethodInfo Method { get; init; }
    public required IReadOnlyList<ToolParameterInfo> ArgumentParameters { get; init; }
    public bool AcceptsContext { get; init; }
    public bool IsAsync { get; init; }

    /// <summary>
    /// When the tool opted in via <see cref="McpToolAttribute.StructuredOutput"/>, this
    /// is the unwrapped return type (Task&lt;T&gt; -> T). Null otherwise.
    /// </summary>
    public Type? StructuredReturnType { get; init; }
}

internal sealed class ToolParameterInfo
{
    public required string ParameterName { get; init; }
    public required Type ParameterType { get; init; }
    public bool IsRequired { get; init; }
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Scans registered tool types at startup and builds ToolRegistration entries via reflection.
/// Registered as a singleton and populated during AddMcpTools().
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, ToolRegistration> _tools =
        new(StringComparer.OrdinalIgnoreCase);

    internal IReadOnlyDictionary<string, ToolRegistration> Tools => _tools;

    internal void Register(Type toolType)
    {
        foreach (var method in toolType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = method.GetCustomAttribute<McpToolAttribute>();
            if (attr == null) continue;

            var toolName = attr.Name ?? ToSnakeCase(method.Name);
            var argumentParams = new List<ToolParameterInfo>();
            bool acceptsContext = false;

            foreach (var p in method.GetParameters())
            {
                var underlyingType = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;

                if (typeof(IEntityContext).IsAssignableFrom(underlyingType))
                {
                    acceptsContext = true;
                    continue;
                }

                var paramAttr = p.GetCustomAttribute<McpParameterAttribute>();
                bool isRequired = paramAttr != null
                    ? paramAttr.Required
                    : !p.IsOptional && !IsNullableReferenceType(p);

                argumentParams.Add(new ToolParameterInfo
                {
                    ParameterName = p.Name!,
                    ParameterType = underlyingType,
                    IsRequired = isRequired,
                    Description = paramAttr?.Description ?? string.Empty
                });
            }

            bool isAsync = typeof(Task).IsAssignableFrom(method.ReturnType);
            Type? structuredReturnType = null;
            ToolInputSchema? outputSchema = null;

            if (attr.StructuredOutput)
            {
                structuredReturnType = UnwrapReturnType(method.ReturnType, isAsync, toolName);
                outputSchema = JsonSchemaBuilder.BuildSchemaFromType(structuredReturnType);
            }

            var metadata = new ToolMetadata
            {
                Name = toolName,
                Description = attr.Description,
                RequiresAuthentication = attr.RequiresAuthentication,
                Deferred = attr.Deferred,
                InputSchema = JsonSchemaBuilder.BuildSchemaFromParameters(argumentParams),
                OutputSchema = outputSchema,
                ExamplePrompts = attr.ExamplePrompts?.ToList()
            };

            _tools[toolName] = new ToolRegistration
            {
                Name = toolName,
                Metadata = metadata,
                DeclaringType = toolType,
                Method = method,
                ArgumentParameters = argumentParams,
                AcceptsContext = acceptsContext,
                IsAsync = isAsync,
                StructuredReturnType = structuredReturnType
            };
        }
    }

    private static Type UnwrapReturnType(Type methodReturnType, bool isAsync, string toolName)
    {
        if (!isAsync) return methodReturnType;
        if (methodReturnType == typeof(Task))
            throw new InvalidOperationException(
                $"Tool '{toolName}' has StructuredOutput=true but returns non-generic Task. " +
                "Return Task<T> with a structurable T.");
        return methodReturnType.GetGenericArguments()[0];
    }

    private static bool IsNullableReferenceType(ParameterInfo p)
    {
        var ctx = new NullabilityInfoContext();
        var info = ctx.Create(p);
        return info.ReadState == NullabilityState.Nullable;
    }

    private static string ToSnakeCase(string name) =>
        Regex.Replace(name, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();
}
