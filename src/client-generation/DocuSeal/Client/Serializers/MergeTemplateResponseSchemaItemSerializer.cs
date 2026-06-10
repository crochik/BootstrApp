using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for MergeTemplateResponseSchemaItem - handles serialization and deserialization
/// </summary>
public static class MergeTemplateResponseSchemaItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a MergeTemplateResponseSchemaItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static MergeTemplateResponseSchemaItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a MergeTemplateResponseSchemaItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static MergeTemplateResponseSchemaItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static MergeTemplateResponseSchemaItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string attachmentUuid = default!;
        string name = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "attachment_uuid":
                    attachmentUuid = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new MergeTemplateResponseSchemaItem
        {
            AttachmentUuid = attachmentUuid!,
            Name = name!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of MergeTemplateResponseSchemaItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<MergeTemplateResponseSchemaItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<MergeTemplateResponseSchemaItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<MergeTemplateResponseSchemaItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a MergeTemplateResponseSchemaItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(MergeTemplateResponseSchemaItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of MergeTemplateResponseSchemaItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<MergeTemplateResponseSchemaItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
