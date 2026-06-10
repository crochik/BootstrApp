using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromDocxRequestDocumentsItem - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromDocxRequestDocumentsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromDocxRequestDocumentsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromDocxRequestDocumentsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromDocxRequestDocumentsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromDocxRequestDocumentsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromDocxRequestDocumentsItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string name = default!;
        string file = default!;
        List<CreateTemplateFromDocxRequestDocumentsItemFieldsItem>? fields = null;

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
                        fields = new List<CreateTemplateFromDocxRequestDocumentsItemFieldsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateTemplateFromDocxRequestDocumentsItemFieldsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                fields.Add(_itemValue);
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateTemplateFromDocxRequestDocumentsItem
        {
            Name = name!,
            File = file!,
            Fields = fields
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateTemplateFromDocxRequestDocumentsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromDocxRequestDocumentsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromDocxRequestDocumentsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromDocxRequestDocumentsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromDocxRequestDocumentsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromDocxRequestDocumentsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromDocxRequestDocumentsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromDocxRequestDocumentsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
