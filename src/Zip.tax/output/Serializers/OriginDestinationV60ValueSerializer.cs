using System.Text.Json;

namespace ZipTax.Models;

/// <summary>
/// Serializer for OriginDestinationV60Value - handles serialization and deserialization
/// </summary>
public static class OriginDestinationV60ValueSerializer
{
    /// <summary>
    /// Gets the Description attribute value for an enum member
    /// </summary>
    private static string? GetEnumDescription(OriginDestinationV60Value value)
    {
        var field = value.GetType().GetField(value.ToString());
        if (field == null)
            return null;

        var attribute = (System.ComponentModel.DescriptionAttribute?)Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute));
        return attribute?.Description ?? value.ToString();
    }

    /// <summary>
    /// Gets the enum value from its Description attribute
    /// </summary>
    private static OriginDestinationV60Value? GetEnumFromDescription(string description)
    {
        var type = typeof(OriginDestinationV60Value);
        foreach (var field in type.GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute)) is System.ComponentModel.DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                    return (OriginDestinationV60Value)field.GetValue(null)!;
            }
        }

        // Fallback: try to parse by name
        return Enum.TryParse<OriginDestinationV60Value>(description, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// Deserializes a JsonElement into a OriginDestinationV60Value enum value using Description attribute
    /// </summary>
    public static OriginDestinationV60Value? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        var stringValue = element.GetString();
        if (string.IsNullOrEmpty(stringValue))
            return null;

        return GetEnumFromDescription(stringValue);
    }

    /// <summary>
    /// Deserializes a JsonDocument into a OriginDestinationV60Value enum value
    /// </summary>
    public static OriginDestinationV60Value? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElement(document.RootElement);
    }

    /// <summary>
    /// Serializes a OriginDestinationV60Value enum value to a JSON string using Description attribute
    /// </summary>
    public static string SerializeToJson(OriginDestinationV60Value value)
    {
        var description = GetEnumDescription(value);
        return JsonSerializer.Serialize(description);
    }

    /// <summary>
    /// Serializes a OriginDestinationV60Value enum value to its string representation (Description value)
    /// </summary>
    public static string SerializeToString(OriginDestinationV60Value value)
    {
        return GetEnumDescription(value) ?? value.ToString();
    }
}
