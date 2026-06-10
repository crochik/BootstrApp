using System.Text.Json;

namespace ZipTax.Models;

/// <summary>
/// Serializer for BaseRateV60JurType - handles serialization and deserialization
/// </summary>
public static class BaseRateV60JurTypeSerializer
{
    /// <summary>
    /// Gets the Description attribute value for an enum member
    /// </summary>
    private static string? GetEnumDescription(BaseRateV60JurType value)
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
    private static BaseRateV60JurType? GetEnumFromDescription(string description)
    {
        var type = typeof(BaseRateV60JurType);
        foreach (var field in type.GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute)) is System.ComponentModel.DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                    return (BaseRateV60JurType)field.GetValue(null)!;
            }
        }

        // Fallback: try to parse by name
        return Enum.TryParse<BaseRateV60JurType>(description, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// Deserializes a JsonElement into a BaseRateV60JurType enum value using Description attribute
    /// </summary>
    public static BaseRateV60JurType? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        var stringValue = element.GetString();
        if (string.IsNullOrEmpty(stringValue))
            return null;

        return GetEnumFromDescription(stringValue);
    }

    /// <summary>
    /// Deserializes a JsonDocument into a BaseRateV60JurType enum value
    /// </summary>
    public static BaseRateV60JurType? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElement(document.RootElement);
    }

    /// <summary>
    /// Serializes a BaseRateV60JurType enum value to a JSON string using Description attribute
    /// </summary>
    public static string SerializeToJson(BaseRateV60JurType value)
    {
        var description = GetEnumDescription(value);
        return JsonSerializer.Serialize(description);
    }

    /// <summary>
    /// Serializes a BaseRateV60JurType enum value to its string representation (Description value)
    /// </summary>
    public static string SerializeToString(BaseRateV60JurType value)
    {
        return GetEnumDescription(value) ?? value.ToString();
    }
}
