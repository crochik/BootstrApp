using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetTemplateResponseFieldsItem - handles serialization and deserialization
/// </summary>
public static class GetTemplateResponseFieldsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetTemplateResponseFieldsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetTemplateResponseFieldsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetTemplateResponseFieldsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetTemplateResponseFieldsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetTemplateResponseFieldsItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string uuid = default!;
        string submitterUuid = default!;
        string name = default!;
        string type = default!;
        bool required = default!;
        GetTemplateResponseFieldsItemPreferences? preferences = default;
        List<GetTemplateResponseFieldsItemAreasItem>? areas = null;

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
                    preferences = GetTemplateResponseFieldsItemPreferencesSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "areas":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        areas = new List<GetTemplateResponseFieldsItemAreasItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetTemplateResponseFieldsItemAreasItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                areas.Add(_itemValue);
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetTemplateResponseFieldsItem
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
    /// Deserializes a JsonDocument into a list of GetTemplateResponseFieldsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetTemplateResponseFieldsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetTemplateResponseFieldsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetTemplateResponseFieldsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetTemplateResponseFieldsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetTemplateResponseFieldsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetTemplateResponseFieldsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetTemplateResponseFieldsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
