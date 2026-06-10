using System.Reflection;
using McpServer.Models;
using McpServer.Resources.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using PI.Shared.Models;

namespace McpServer.Resources.Registry;

/// <summary>
/// Pre-computed description of a single discovered MCP resource action.
/// </summary>
internal sealed class ResourceRegistration
{
    public required string Uri { get; init; }
    public required ResourceMetadata Metadata { get; init; }
    public required Type ControllerType { get; init; }
    public required MethodInfo Method { get; init; }
    public required bool AcceptsContext { get; init; }
    public required bool IsAsync { get; init; }
}

/// <summary>
/// Discovers <see cref="McpResourceAttribute"/>-decorated controller actions via
/// <see cref="IActionDescriptorCollectionProvider"/> and caches a URI-keyed map of
/// registrations. Singleton — discovery runs once on first resolution.
/// </summary>
public sealed class ResourceRegistry
{
    private readonly Dictionary<string, ResourceRegistration> _resources =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<ResourceMetadata> _metadata = new();

    internal IReadOnlyDictionary<string, ResourceRegistration> Resources => _resources;
    internal IReadOnlyList<ResourceMetadata> Metadata => _metadata;

    public ResourceRegistry(IActionDescriptorCollectionProvider actionDescriptors)
    {
        foreach (var descriptor in actionDescriptors.ActionDescriptors.Items)
        {
            if (descriptor is not ControllerActionDescriptor cad) continue;

            var attr = cad.MethodInfo.GetCustomAttribute<McpResourceAttribute>();
            if (attr == null) continue;

            Register(cad, attr);
        }
    }

    private void Register(ControllerActionDescriptor cad, McpResourceAttribute attr)
    {
        var controllerName = cad.ControllerTypeInfo.FullName ?? cad.ControllerName;
        var actionId = $"{controllerName}.{cad.MethodInfo.Name}";

        // GET only.
        var httpMethods = cad.MethodInfo.GetCustomAttributes()
            .OfType<IActionHttpMethodProvider>()
            .SelectMany(a => a.HttpMethods)
            .ToList();
        if (httpMethods.Count == 0 || httpMethods.Any(m => !string.Equals(m, "GET", StringComparison.OrdinalIgnoreCase)))
        {
            var verbs = httpMethods.Count == 0 ? "(no HTTP verb)" : string.Join(",", httpMethods);
            throw new InvalidOperationException(
                $"[McpResource] is only valid on GET actions; '{actionId}' has '{verbs}'.");
        }

        // URI: attribute override, else route template.
        var uri = attr.Uri ?? cad.AttributeRouteInfo?.Template;
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new InvalidOperationException(
                $"[McpResource] on '{actionId}' has no Uri and the action has no route template.");
        }
        if (uri.Contains('{'))
        {
            throw new InvalidOperationException(
                $"[McpResource] cannot bind to parameterized route '{uri}' on '{actionId}'. " +
                "Resource templates are not supported.");
        }

        // Parameter validation: only IEntityContext is allowed.
        var parameters = cad.MethodInfo.GetParameters();
        bool acceptsContext = false;
        foreach (var p in parameters)
        {
            var underlying = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
            if (typeof(IEntityContext).IsAssignableFrom(underlying))
            {
                if (acceptsContext)
                {
                    throw new InvalidOperationException(
                        $"[McpResource] method '{actionId}' declares more than one IEntityContext parameter.");
                }
                acceptsContext = true;
                continue;
            }

            throw new InvalidOperationException(
                $"[McpResource] method '{actionId}' has parameter '{p.Name}' that is not IEntityContext. " +
                "Resources do not accept user arguments.");
        }

        // Return type validation.
        bool isAsync = typeof(Task).IsAssignableFrom(cad.MethodInfo.ReturnType);
        var payloadType = UnwrapTaskType(cad.MethodInfo.ReturnType, isAsync, actionId);
        ValidateReturnType(payloadType, actionId);

        // Duplicate URI check.
        if (_resources.TryGetValue(uri, out var existing))
        {
            var existingId = $"{existing.ControllerType.FullName}.{existing.Method.Name}";
            throw new InvalidOperationException(
                $"Duplicate [McpResource] URI '{uri}' on '{actionId}'; already registered by '{existingId}'.");
        }

        var metadata = new ResourceMetadata
        {
            Uri = uri,
            Name = attr.Name ?? cad.MethodInfo.Name,
            Description = string.IsNullOrEmpty(attr.Description) ? null : attr.Description,
            MimeType = attr.MimeType,
            RequiresAuthentication = attr.RequiresAuthentication
        };

        _resources[uri] = new ResourceRegistration
        {
            Uri = uri,
            Metadata = metadata,
            ControllerType = cad.ControllerTypeInfo.AsType(),
            Method = cad.MethodInfo,
            AcceptsContext = acceptsContext,
            IsAsync = isAsync
        };
        _metadata.Add(metadata);
    }

    private static Type UnwrapTaskType(Type returnType, bool isAsync, string actionId)
    {
        if (!isAsync) return returnType;
        if (returnType == typeof(Task))
        {
            throw new InvalidOperationException(
                $"[McpResource] method '{actionId}' returns non-generic Task. " +
                "Return Task<T> with T being string, byte[], ResourceContent, IEnumerable<ResourceContent>, or ResourceReadResult.");
        }
        return returnType.GetGenericArguments()[0];
    }

    private static void ValidateReturnType(Type t, string actionId)
    {
        if (t == typeof(void))
        {
            throw new InvalidOperationException(
                $"[McpResource] method '{actionId}' returns void. Resources must return a payload.");
        }

        // Reject MVC pipeline shapes — they require filters/formatters that don't
        // run on the MCP path. Consumers can extract a sibling method that returns
        // the typed payload directly.
        if (typeof(IActionResult).IsAssignableFrom(t) ||
            typeof(IConvertToActionResult).IsAssignableFrom(t))
        {
            throw new InvalidOperationException(
                $"[McpResource] method '{actionId}' returns '{t.Name}', which is an MVC pipeline shape. " +
                "Return string, byte[], ResourceContent, IEnumerable<ResourceContent>, ResourceReadResult, " +
                "or any serializable payload type instead.");
        }
    }
}
