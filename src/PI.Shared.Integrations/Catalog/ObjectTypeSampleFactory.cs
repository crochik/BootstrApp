using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.Shared.Integrations.Catalog;

/// <summary>
/// Builds example payloads from an account's <c>ObjectType</c> definition: one entry
/// per declared field, with a plausible, deterministic value inferred from the field
/// name. Deterministic output keeps an integration's "test trigger" step stable across
/// calls. The envelope mirrors <see cref="Delivery.WebhookPayload"/> so a sample looks
/// exactly like a real delivery.
/// </summary>
public sealed class ObjectTypeSampleFactory : ISampleFactory
{
    // Fixed reference instant so repeated samples are byte-for-byte identical.
    private static readonly DateTimeOffset SampleInstant = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    private readonly ObjectTypeService _objectTypes;

    public ObjectTypeSampleFactory(ObjectTypeService objectTypes)
    {
        _objectTypes = objectTypes;
    }

    public async Task<IDictionary<string, object?>?> CreateDataAsync(IEntityContext context, string objectKey)
    {
        var objectType = await _objectTypes.GetAsync(context, objectKey);
        if (objectType is null) return null;

        var data = new Dictionary<string, object?>
        {
            [Model.IdFieldName] = "00000000-0000-0000-0000-000000000001",
        };

        if (objectType.Fields is not null)
        {
            foreach (var name in objectType.Fields.Keys)
            {
                if (string.Equals(name, Model.IdFieldName, StringComparison.OrdinalIgnoreCase)) continue;
                data[name] = SampleValue(name);
            }
        }

        return data;
    }

    public async Task<IDictionary<string, object?>?> CreateDeliveredSampleAsync(IEntityContext context, string objectKey, string eventKey, string tenant)
    {
        var data = await CreateDataAsync(context, objectKey);
        if (data is null) return null;

        return new Dictionary<string, object?>
        {
            ["eventId"] = $"evt_{objectKey}_{eventKey}_sample".ToLowerInvariant(),
            ["tenantId"] = tenant,
            ["eventName"] = $"{objectKey}.{eventKey}",
            ["occurredAt"] = SampleInstant.ToString("O"),
            ["schemaVersion"] = "1",
            ["data"] = data,
        };
    }

    // Plausible value from the field name; intentionally simple — the goal is a
    // recognizable shape for field-mapping, not a faithful per-type value.
    private static object SampleValue(string name)
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
            _ when n.Contains("amount") || n.Contains("price") || n.Contains("total") => 19.99m,
            _ when n.Contains("count") || n.Contains("quantity") || n.Contains("qty") => 42,
            _ when n.Contains("date") || n.Contains("on") => SampleInstant.ToString("O"),
            _ when n.EndsWith("id") => "1001",
            _ => $"sample-{n}",
        };
    }
}
