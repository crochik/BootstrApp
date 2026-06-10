using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpServer.Models;
using McpServer.Tools.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace McpServer.Services;

/// <summary>
/// IToolSource implementation that serves tools discovered via [McpTool] attributes
/// on classes registered through McpToolsBuilder.AddToolType&lt;T&gt;().
/// </summary>
public sealed class AttributeToolSource : IToolSource
{
    private readonly ToolRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AttributeToolSource> _logger;

    // Tool schemas advertise enums as JSON strings (member names), so callers send
    // them that way and tool return values must serialize the same way to round-trip.
    // The default EnumConverter only handles numeric values, so we register
    // JsonStringEnumConverter on every path that carries tool args or results.
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
    };

    private static readonly JsonSerializerOptions ArgumentJson = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
    };

    public AttributeToolSource(
        ToolRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<AttributeToolSource> logger)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task<IReadOnlyList<ToolMetadata>> GetToolsAsync()
    {
        _logger.LogDebug("Returning {Count} attribute-registered tools", _registry.Tools.Count);
        return Task.FromResult<IReadOnlyList<ToolMetadata>>(
            _registry.Tools.Values.Select(r => r.Metadata).ToList());
    }

    public async Task<ToolCallResult?> TryExecuteAsync(
        IEntityContext? context,
        string toolName,
        Dictionary<string, object>? arguments)
    {
        if (!_registry.Tools.TryGetValue(toolName, out var registration))
            return null; // not ours — composite will try next source

        _logger.LogInformation("Executing tool: {ToolName}", toolName);

        if (registration.Metadata.RequiresAuthentication && context == null)
            return ToolCallResult.Error("Authentication required.");

        try
        {
            var toolInstance = _serviceProvider.GetRequiredService(registration.DeclaringType);
            var invokeArgs = BuildInvokeArgs(registration, context, arguments);

            object? returnValue;
            if (registration.IsAsync)
            {
                var task = (Task)registration.Method.Invoke(toolInstance, invokeArgs)!;
                await task.ConfigureAwait(false);

                var taskType = task.GetType();
                returnValue = taskType.IsGenericType
                    ? taskType.GetProperty("Result")!.GetValue(task)
                    : null;
            }
            else
            {
                returnValue = registration.Method.Invoke(toolInstance, invokeArgs);
            }

            return NormalizeResult(registration, returnValue);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is McpToolException tool)
        {
            _logger.LogInformation("Tool {ToolName} reported error: {Message}", toolName, tool.Message);
            return new ToolCallResult
            {
                IsError = true,
                Content = tool.Content,
                StructuredContent = tool.StructuredContent
            };
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            _logger.LogError(tie.InnerException, "Tool {ToolName} threw unexpectedly", toolName);
            return ToolCallResult.Error($"Tool '{toolName}' failed unexpectedly. See server logs.");
        }
        catch (InvalidOperationException ex)
        {
            // BuildInvokeArgs throws these for missing required params or type-conversion
            // failures; messages are intentionally user-facing so the LLM can fix its call.
            _logger.LogWarning(ex, "Argument binding failed for tool {ToolName}", toolName);
            return ToolCallResult.Error(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            return ToolCallResult.Error($"Tool '{toolName}' failed unexpectedly. See server logs.");
        }
    }

    private static object?[] BuildInvokeArgs(
        ToolRegistration registration,
        IEntityContext? context,
        Dictionary<string, object>? arguments)
    {
        var methodParams = registration.Method.GetParameters();
        var invokeArgs = new object?[methodParams.Length];
        int argParamIndex = 0;

        for (int i = 0; i < methodParams.Length; i++)
        {
            var p = methodParams[i];
            var underlyingType = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;

            if (typeof(IEntityContext).IsAssignableFrom(underlyingType))
            {
                invokeArgs[i] = context;
                continue;
            }

            var paramInfo = registration.ArgumentParameters[argParamIndex++];

            if (arguments != null && arguments.TryGetValue(paramInfo.ParameterName, out var rawValue))
            {
                invokeArgs[i] = ConvertArgument(rawValue, paramInfo.ParameterType, paramInfo.ParameterName);
            }
            else if (paramInfo.IsRequired)
            {
                throw new InvalidOperationException($"Required parameter '{paramInfo.ParameterName}' is missing.");
            }
            else
            {
                invokeArgs[i] = p.HasDefaultValue ? p.DefaultValue : null;
            }
        }

        return invokeArgs;
    }

    private static object? ConvertArgument(object rawValue, Type targetType, string paramName)
    {
        if (rawValue is JsonElement element)
        {
            try
            {
                return element.Deserialize(targetType, ArgumentJson);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Cannot convert parameter '{paramName}' to {targetType.Name}: {ex.Message}", ex);
            }
        }

        if (targetType.IsInstanceOfType(rawValue))
            return rawValue;

        try
        {
            return Convert.ChangeType(rawValue, targetType);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot convert parameter '{paramName}' (value: {rawValue}) to {targetType.Name}: {ex.Message}", ex);
        }
    }

    private static ToolCallResult NormalizeResult(ToolRegistration registration, object? value)
    {
        var hasOutputSchema = registration.Metadata.OutputSchema != null;

        if (hasOutputSchema && value != null)
            return ToolCallResult.Structured(value);

        return value switch
        {
            null => ToolCallResult.Text("(no result)"),
            string s => ToolCallResult.Text(s),
            ToolCallResult r => r,
            _ => ToolCallResult.Text(JsonSerializer.Serialize(value, PrettyJson))
        };
    }
}
