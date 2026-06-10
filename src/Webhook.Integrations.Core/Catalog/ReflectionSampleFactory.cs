using System.Reflection;

namespace Webhook.Integrations.Core.Catalog;

/// <summary>
/// Generates example payloads by reflecting over an object's CLR type and inventing
/// plausible, deterministic values from each property's name and type. Deterministic
/// output keeps an integration's "test trigger" step stable across calls.
/// </summary>
public sealed class ReflectionSampleFactory : ISampleFactory
{
    // Fixed reference instant so repeated samples are byte-for-byte identical.
    private static readonly DateTimeOffset SampleInstant =
        new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    public IDictionary<string, object?> CreateData(TriggerObjectDescriptor descriptor) => BuildData(descriptor);

    public IDictionary<string, object?> CreateDeliveredSample(TriggerObjectDescriptor descriptor, string eventKey, string tenant)
    {
        // Mirrors Webhook.Publisher's WebhookPayload envelope. Integrations de-duplicate
        // triggers on the top-level eventId (delivered as the Webhook-Id header).
        return new Dictionary<string, object?>
        {
            ["eventId"] = $"evt_{descriptor.Key}_{eventKey}_sample",
            ["tenantId"] = tenant,
            ["eventName"] = $"{descriptor.Key}.{eventKey}",
            ["occurredAt"] = SampleInstant.ToString("O"),
            ["schemaVersion"] = "1",
            ["data"] = BuildData(descriptor),
        };
    }

    private static Dictionary<string, object?> BuildData(TriggerObjectDescriptor descriptor)
    {
        var data = new Dictionary<string, object?>();
        foreach (var prop in descriptor.ClrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            data[CamelCase(prop.Name)] = SampleValue(prop.Name, prop.PropertyType);
        }

        // Guarantee an id even if the type didn't declare one.
        if (!data.ContainsKey("id"))
        {
            data["id"] = $"{descriptor.Key}_1001";
        }

        return data;
    }

    private static object? SampleValue(string name, Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying.IsEnum)
        {
            var values = Enum.GetValues(underlying);
            return values.Length > 0 ? values.GetValue(0)?.ToString() : null;
        }

        if (underlying == typeof(string))
        {
            return SampleString(name);
        }

        if (underlying == typeof(bool))
        {
            return true;
        }

        if (underlying == typeof(Guid))
        {
            return "00000000-0000-0000-0000-000000000001";
        }

        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
        {
            return SampleInstant.ToString("O");
        }

        if (underlying == typeof(decimal) || underlying == typeof(double) || underlying == typeof(float))
        {
            return 19.99m;
        }

        if (underlying == typeof(byte) || underlying == typeof(short) || underlying == typeof(int) || underlying == typeof(long))
        {
            return 42;
        }

        // Unknown/complex types are omitted rather than guessed.
        return null;
    }

    private static string SampleString(string name)
    {
        var n = name.ToLowerInvariant();
        return n switch
        {
            _ when n.Contains("email") => "jane.doe@example.com",
            _ when n.Contains("phone") => "+15551234567",
            _ when n.Contains("url") || n.Contains("website") => "https://example.com",
            _ when n.Contains("firstname") => "Jane",
            _ when n.Contains("lastname") => "Doe",
            _ when n.Contains("name") => "Jane Doe",
            _ when n.Contains("company") || n.Contains("account") => "Acme, Inc.",
            _ when n.Contains("status") || n.Contains("stage") => "open",
            _ when n.Contains("currency") => "USD",
            _ when n is "id" || n.EndsWith("id") => "1001",
            _ => $"sample-{ReflectionEventCatalog.Normalize(name)}",
        };
    }

    private static string CamelCase(string name) =>
        name.Length == 0 || char.IsLower(name[0])
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
}
