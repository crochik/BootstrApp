using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromDocxResponseFieldsItemAreasItem - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromDocxResponseFieldsItemAreasItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromDocxResponseFieldsItemAreasItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromDocxResponseFieldsItemAreasItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromDocxResponseFieldsItemAreasItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromDocxResponseFieldsItemAreasItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromDocxResponseFieldsItemAreasItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        double x = default!;
        double y = default!;
        double w = default!;
        double h = default!;
        string attachmentUuid = default!;
        int page = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "x":
                    x = BaseSerializer.ParsePrimitivedouble(property.Value);
                    break;
                case "y":
                    y = BaseSerializer.ParsePrimitivedouble(property.Value);
                    break;
                case "w":
                    w = BaseSerializer.ParsePrimitivedouble(property.Value);
                    break;
                case "h":
                    h = BaseSerializer.ParsePrimitivedouble(property.Value);
                    break;
                case "attachment_uuid":
                    attachmentUuid = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "page":
                    page = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateTemplateFromDocxResponseFieldsItemAreasItem
        {
            X = x!,
            Y = y!,
            W = w!,
            H = h!,
            AttachmentUuid = attachmentUuid!,
            Page = page!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateTemplateFromDocxResponseFieldsItemAreasItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromDocxResponseFieldsItemAreasItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromDocxResponseFieldsItemAreasItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromDocxResponseFieldsItemAreasItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromDocxResponseFieldsItemAreasItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromDocxResponseFieldsItemAreasItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromDocxResponseFieldsItemAreasItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromDocxResponseFieldsItemAreasItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
