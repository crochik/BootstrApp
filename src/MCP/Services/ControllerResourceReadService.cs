using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpServer.Models;
using McpServer.Resources.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace McpServer.Services;

/// <summary>
/// Invokes a controller action discovered as an MCP resource. Resolves the
/// controller through DI per call (controllers are typically Transient),
/// passes <see cref="IEntityContext"/> if the method declares one, and
/// normalizes the return value to <see cref="ResourceReadResult"/>.
/// MVC filters and <c>[Authorize]</c> policies do NOT run on this path —
/// auth is gated separately by <see cref="Resources.Attributes.McpResourceAttribute.RequiresAuthentication"/>.
/// </summary>
public sealed class ControllerResourceReadService : IResourceReadService
{
    private readonly ResourceRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ControllerResourceReadService> _logger;

    private static readonly JsonSerializerOptions PayloadJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
    };

    public ControllerResourceReadService(
        ResourceRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<ControllerResourceReadService> logger)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<ResourceReadResult> ReadResourceAsync(IEntityContext? context, string uri)
    {
        if (!_registry.Resources.TryGetValue(uri, out var registration))
            return ResourceReadResult.Error($"Resource '{uri}' not found.");

        _logger.LogInformation("Reading resource: {Uri}", uri);

        if (registration.Metadata.RequiresAuthentication && context == null)
            return ResourceReadResult.Error($"Authentication required for resource '{uri}'.");

        try
        {
            var controller = ActivatorUtilities.CreateInstance(_serviceProvider, registration.ControllerType);
            var args = registration.AcceptsContext ? new object?[] { context } : Array.Empty<object?>();

            object? raw;
            if (registration.IsAsync)
            {
                var task = (Task)registration.Method.Invoke(controller, args)!;
                await task.ConfigureAwait(false);

                var taskType = task.GetType();
                raw = taskType.IsGenericType
                    ? taskType.GetProperty("Result")!.GetValue(task)
                    : null;
            }
            else
            {
                raw = registration.Method.Invoke(controller, args);
            }

            return Normalize(registration, raw);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is InvalidOperationException inner)
        {
            _logger.LogWarning(inner, "Resource {Uri} reported error", uri);
            return ResourceReadResult.Error(inner.Message);
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            _logger.LogError(tie.InnerException, "Resource {Uri} threw unexpectedly", uri);
            return ResourceReadResult.Error($"Resource '{uri}' failed unexpectedly. See server logs.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading resource {Uri}", uri);
            return ResourceReadResult.Error($"Resource '{uri}' failed unexpectedly. See server logs.");
        }
    }

    private static ResourceReadResult Normalize(ResourceRegistration reg, object? value)
    {
        if (value == null)
            return ResourceReadResult.Error("Resource produced no content.");

        switch (value)
        {
            case ResourceReadResult result:
                return result;

            case string s:
                return ResourceReadResult.Text(reg.Uri, s, reg.Metadata.MimeType ?? "text/plain");

            case byte[] bytes:
                return ResourceReadResult.Blob(reg.Uri, bytes, reg.Metadata.MimeType ?? "application/octet-stream");

            case ResourceContent single:
                if (string.IsNullOrEmpty(single.Uri)) single.Uri = reg.Uri;
                if (single.MimeType == null) single.MimeType = reg.Metadata.MimeType;
                return new ResourceReadResult { Contents = [single] };

            case IEnumerable<ResourceContent> many:
                var list = many.ToList();
                foreach (var c in list)
                {
                    if (string.IsNullOrEmpty(c.Uri)) c.Uri = reg.Uri;
                    if (c.MimeType == null) c.MimeType = reg.Metadata.MimeType;
                }
                return new ResourceReadResult { Contents = list };

            default:
                // Strongly-typed payload returned by controller (e.g. ConfigDto).
                // Serialize to JSON and emit as text content.
                var json = JsonSerializer.Serialize(value, PayloadJson);
                return ResourceReadResult.Text(reg.Uri, json, reg.Metadata.MimeType ?? "application/json");
        }
    }
}
