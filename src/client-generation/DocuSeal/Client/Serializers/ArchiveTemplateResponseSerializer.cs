using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for ArchiveTemplateResponse - handles serialization and deserialization
/// </summary>
public static class ArchiveTemplateResponseSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a ArchiveTemplateResponse instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static ArchiveTemplateResponse? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a ArchiveTemplateResponse instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static ArchiveTemplateResponse? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static ArchiveTemplateResponse DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        string archivedAt = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    id = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "archived_at":
                    archivedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new ArchiveTemplateResponse
        {
            Id = id!,
            ArchivedAt = archivedAt!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of ArchiveTemplateResponse instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<ArchiveTemplateResponse> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<ArchiveTemplateResponse>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<ArchiveTemplateResponse>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a ArchiveTemplateResponse instance to a JSON string
    /// </summary>
    public static string SerializeToJson(ArchiveTemplateResponse instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of ArchiveTemplateResponse instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<ArchiveTemplateResponse> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
