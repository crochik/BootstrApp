using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromPdfRequestDocumentsItem - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromPdfRequestDocumentsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromPdfRequestDocumentsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromPdfRequestDocumentsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromPdfRequestDocumentsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromPdfRequestDocumentsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromPdfRequestDocumentsItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string name = default!;
        string file = default!;
        List<CreateTemplateFromPdfRequestDocumentsItemFieldsItem>? fields = null;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "file":
                    file = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "fields":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        fields = new List<CreateTemplateFromPdfRequestDocumentsItemFieldsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateTemplateFromPdfRequestDocumentsItemFieldsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                fields.Add(_itemValue);
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateTemplateFromPdfRequestDocumentsItem
        {
            Name = name!,
            File = file!,
            Fields = fields
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateTemplateFromPdfRequestDocumentsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromPdfRequestDocumentsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromPdfRequestDocumentsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromPdfRequestDocumentsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromPdfRequestDocumentsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromPdfRequestDocumentsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromPdfRequestDocumentsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromPdfRequestDocumentsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
