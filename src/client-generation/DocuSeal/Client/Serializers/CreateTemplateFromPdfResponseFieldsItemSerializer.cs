using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromPdfResponseFieldsItem - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromPdfResponseFieldsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromPdfResponseFieldsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromPdfResponseFieldsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromPdfResponseFieldsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromPdfResponseFieldsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromPdfResponseFieldsItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string uuid = default!;
        string submitterUuid = default!;
        string name = default!;
        string type = default!;
        bool required = default!;
        CreateTemplateFromPdfResponseFieldsItemPreferences? preferences = default;
        List<CreateTemplateFromPdfResponseFieldsItemAreasItem>? areas = null;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "uuid":
                    uuid = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "submitter_uuid":
                    submitterUuid = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "type":
                    type = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "required":
                    required = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "preferences":
                    preferences = CreateTemplateFromPdfResponseFieldsItemPreferencesSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "areas":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        areas = new List<CreateTemplateFromPdfResponseFieldsItemAreasItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateTemplateFromPdfResponseFieldsItemAreasItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                areas.Add(_itemValue);
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateTemplateFromPdfResponseFieldsItem
        {
            Uuid = uuid!,
            SubmitterUuid = submitterUuid!,
            Name = name!,
            Type = type!,
            Required = required!,
            Preferences = preferences,
            Areas = areas!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateTemplateFromPdfResponseFieldsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromPdfResponseFieldsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromPdfResponseFieldsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromPdfResponseFieldsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromPdfResponseFieldsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromPdfResponseFieldsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromPdfResponseFieldsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromPdfResponseFieldsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
