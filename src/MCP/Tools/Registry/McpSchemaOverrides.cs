using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace McpServer.Tools.Registry;

/// <summary>
/// Global registry that lets a host replace the reflection-derived JSON Schema
/// for a CLR type with an explicit schema string. Keyed by <see cref="Type.FullName"/>.
/// Lookups happen during tool registration (in <c>JsonSchemaBuilder</c>), so register
/// overrides before <c>AddMcpTools</c> runs.
/// </summary>
public static class McpSchemaOverrides
{
    private static readonly ConcurrentDictionary<string, string> _overrides =
        new(StringComparer.Ordinal);

    public static void Register<T>(string jsonSchema) => Register(typeof(T), jsonSchema);

    /// <summary>
    /// Register a schema override for <paramref name="type"/>. The schema string must
    /// parse as a JSON object. Replaces any prior override for the same type.
    /// </summary>
    public static void Register(Type type, string jsonSchema)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (string.IsNullOrWhiteSpace(jsonSchema))
            throw new ArgumentException("Schema must be a non-empty JSON string.", nameof(jsonSchema));
        if (string.IsNullOrEmpty(type.FullName))
            throw new ArgumentException(
                $"Type '{type}' has no FullName and cannot be used as an override key.", nameof(type));

        using (var doc = JsonDocument.Parse(jsonSchema))
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException(
                    "Schema override must be a JSON object.", nameof(jsonSchema));
        }

        _overrides[type.FullName] = jsonSchema;
    }

    public static bool Unregister(Type type)
    {
        if (type?.FullName is not { } key) return false;
        return _overrides.TryRemove(key, out _);
    }

    public static void Clear() => _overrides.Clear();

    /// <summary>
    /// Returns a fresh <see cref="JsonNode"/> tree for the override, or null if none
    /// is registered. A new tree is parsed on each call so callers can safely mutate
    /// (e.g. inject a per-parameter description) without affecting the registry.
    /// </summary>
    internal static JsonNode? GetClonedNode(Type type)
    {
        if (type?.FullName is not { } key) return null;
        if (!_overrides.TryGetValue(key, out var raw)) return null;
        return JsonNode.Parse(raw);
    }
}
