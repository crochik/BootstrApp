using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

namespace Webhook.Integrations.Core.Catalog;

/// <summary>
/// Builds the catalog by scanning assemblies for <see cref="TriggerObjectAttribute"/>
/// types. Discovery runs once at construction; the result is immutable. Because the
/// list is derived from the loaded types, adding a new decorated class is the only
/// step needed to expose a new object — nothing here, in the controllers, or in the
/// integration definitions enumerates objects by hand.
/// </summary>
public sealed class ReflectionEventCatalog : IEventCatalog
{
    // Conventional lifecycle used when a type declares no explicit events. The
    // description carries a "{0}" slot filled with the object's label.
    private static readonly (string Key, string Label, string Description)[] DefaultLifecycle =
    {
        ("created", "Created", "Fires when a {0} is created."),
        ("updated", "Updated", "Fires when a {0} is updated."),
        ("deleted", "Deleted", "Fires when a {0} is deleted."),
    };

    private readonly Dictionary<string, TriggerObjectDescriptor> _byKey;
    private readonly IReadOnlyList<TriggerObjectDescriptor> _ordered;

    public ReflectionEventCatalog(IEnumerable<Assembly> assemblies)
    {
        _byKey = new Dictionary<string, TriggerObjectDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in assemblies.Distinct().SelectMany(SafeGetTypes))
        {
            var objectAttr = type.GetCustomAttribute<TriggerObjectAttribute>();
            if (objectAttr is null)
            {
                continue;
            }

            var key = Normalize(objectAttr.Key ?? type.Name);
            var label = objectAttr.Label ?? Humanize(type.Name);
            var noun = objectAttr.Noun ?? label;
            var description = objectAttr.Description ?? $"A {label}.";

            var declared = type.GetCustomAttributes<TriggerEventAttribute>()
                .Select(e => new TriggerEventDescriptor(
                    Normalize(e.Key),
                    e.Label ?? $"{noun} {Humanize(e.Key)}",
                    e.Description ?? $"Fires when a {label} \"{Normalize(e.Key)}\" event occurs."))
                .ToList();

            var events = declared.Count > 0
                ? declared
                : DefaultLifecycle
                    .Select(e => new TriggerEventDescriptor(e.Key, $"{noun} {e.Label}", string.Format(e.Description, label)))
                    .ToList();

            // Last decorated type wins on a key collision; this keeps startup deterministic.
            _byKey[key] = new TriggerObjectDescriptor(key, label, noun, description, events, type);
        }

        _ordered = _byKey.Values.OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<TriggerObjectDescriptor> GetObjects() => _ordered;

    public bool TryGetObject(string objectKey, [NotNullWhen(true)] out TriggerObjectDescriptor? descriptor)
    {
        if (objectKey is not null && _byKey.TryGetValue(objectKey, out var found))
        {
            descriptor = found;
            return true;
        }

        descriptor = null;
        return false;
    }

    public bool TryGetEvent(string objectKey, string eventKey, [NotNullWhen(true)] out TriggerEventDescriptor? descriptor)
    {
        if (TryGetObject(objectKey, out var obj))
        {
            descriptor = obj.Events.FirstOrDefault(e => string.Equals(e.Key, eventKey, StringComparison.OrdinalIgnoreCase));
            return descriptor is not null;
        }

        descriptor = null;
        return false;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        // A partially-loadable assembly still yields the types that did load.
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Select(t => t!);
        }
    }

    // "MockContact" / "mock_contact" / "stage changed" -> lower key suitable for URLs
    // and integration field keys.
    internal static string Normalize(string raw)
    {
        var trimmed = raw.Trim();
        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else if (sb.Length > 0 && sb[^1] != '_')
            {
                sb.Append('_');
            }
        }

        return sb.ToString().Trim('_');
    }

    // "MockContactRecord" -> "Mock Contact Record"; tolerant of acronyms and digits.
    internal static string Humanize(string raw)
    {
        var sb = new StringBuilder(raw.Length + 4);
        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (ch is '_' or '-')
            {
                sb.Append(' ');
                continue;
            }

            var boundary = i > 0 && char.IsUpper(ch) &&
                           (char.IsLower(raw[i - 1]) || (i + 1 < raw.Length && char.IsLower(raw[i + 1])));
            if (boundary && sb.Length > 0 && sb[^1] != ' ')
            {
                sb.Append(' ');
            }

            sb.Append(sb.Length == 0 || sb[^1] == ' ' ? char.ToUpperInvariant(ch) : ch);
        }

        return sb.ToString().Trim();
    }
}
