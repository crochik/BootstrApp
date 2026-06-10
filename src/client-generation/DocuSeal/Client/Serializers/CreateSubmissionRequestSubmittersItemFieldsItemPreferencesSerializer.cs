using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionRequestSubmittersItemFieldsItemPreferences - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionRequestSubmittersItemFieldsItemPreferencesSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionRequestSubmittersItemFieldsItemPreferences instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionRequestSubmittersItemFieldsItemPreferences? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionRequestSubmittersItemFieldsItemPreferences instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionRequestSubmittersItemFieldsItemPreferences? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionRequestSubmittersItemFieldsItemPreferences DeserializeFromElementCore(JsonElement element)
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
        return new CreateSubmissionRequestSubmittersItemFieldsItemPreferences
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
    /// Deserializes a JsonDocument into a list of CreateSubmissionRequestSubmittersItemFieldsItemPreferences instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionRequestSubmittersItemFieldsItemPreferences> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionRequestSubmittersItemFieldsItemPreferences>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionRequestSubmittersItemFieldsItemPreferences>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionRequestSubmittersItemFieldsItemPreferences instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionRequestSubmittersItemFieldsItemPreferences instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionRequestSubmittersItemFieldsItemPreferences instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionRequestSubmittersItemFieldsItemPreferences> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
