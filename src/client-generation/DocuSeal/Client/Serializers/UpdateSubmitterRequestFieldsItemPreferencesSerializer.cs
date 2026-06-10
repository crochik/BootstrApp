using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for UpdateSubmitterRequestFieldsItemPreferences - handles serialization and deserialization
/// </summary>
public static class UpdateSubmitterRequestFieldsItemPreferencesSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a UpdateSubmitterRequestFieldsItemPreferences instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static UpdateSubmitterRequestFieldsItemPreferences? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a UpdateSubmitterRequestFieldsItemPreferences instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static UpdateSubmitterRequestFieldsItemPreferences? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static UpdateSubmitterRequestFieldsItemPreferences DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int? fontSize = default;
        string? fontType = default;
        string? font = default;
        string? color = default;
        string? background = default;
        string? align = default;
        string? valign = default;
        string? format = default;
        double? price = default;
        string? currency = default;
        object? mask = default;
        List<string>? reasons = null;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "font_size":
                    fontSize = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "font_type":
                    fontType = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "font":
                    font = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "color":
                    color = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "background":
                    background = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "align":
                    align = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "valign":
                    valign = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "format":
                    format = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "price":
                    price = BaseSerializer.ParsePrimitivedouble(property.Value);
                    break;
                case "currency":
                    currency = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "mask":
                    mask = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
                case "reasons":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        reasons = new List<string>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            reasons.Add(BaseSerializer.ParsePrimitivestring(_item));
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new UpdateSubmitterRequestFieldsItemPreferences
        {
            FontSize = fontSize,
            FontType = fontType,
            Font = font,
            Color = color,
            Background = background,
            Align = align,
            Valign = valign,
            Format = format,
            Price = price,
            Currency = currency,
            Mask = mask,
            Reasons = reasons
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of UpdateSubmitterRequestFieldsItemPreferences instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<UpdateSubmitterRequestFieldsItemPreferences> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<UpdateSubmitterRequestFieldsItemPreferences>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<UpdateSubmitterRequestFieldsItemPreferences>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a UpdateSubmitterRequestFieldsItemPreferences instance to a JSON string
    /// </summary>
    public static string SerializeToJson(UpdateSubmitterRequestFieldsItemPreferences instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of UpdateSubmitterRequestFieldsItemPreferences instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<UpdateSubmitterRequestFieldsItemPreferences> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
