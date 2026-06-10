using System.Text.Json;

namespace ZipTax.Models;

/// <summary>
/// Serializer for AddressDetailV60Incorporated - handles serialization and deserialization
/// </summary>
public static class AddressDetailV60IncorporatedSerializer
{
    /// <summary>
    /// Gets the Description attribute value for an enum member
    /// </summary>
    private static string? GetEnumDescription(AddressDetailV60Incorporated value)
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
    private static AddressDetailV60Incorporated? GetEnumFromDescription(string description)
    {
        var type = typeof(AddressDetailV60Incorporated);
        foreach (var field in type.GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute)) is System.ComponentModel.DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                    return (AddressDetailV60Incorporated)field.GetValue(null)!;
            }
        }

        // Fallback: try to parse by name
        return Enum.TryParse<AddressDetailV60Incorporated>(description, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// Deserializes a JsonElement into a AddressDetailV60Incorporated enum value using Description attribute
    /// </summary>
    public static AddressDetailV60Incorporated? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        var stringValue = element.GetString();
        if (string.IsNullOrEmpty(stringValue))
            return null;

        return GetEnumFromDescription(stringValue);
    }

    /// <summary>
    /// Deserializes a JsonDocument into a AddressDetailV60Incorporated enum value
    /// </summary>
    public static AddressDetailV60Incorporated? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElement(document.RootElement);
    }

    /// <summary>
    /// Serializes a AddressDetailV60Incorporated enum value to a JSON string using Description attribute
    /// </summary>
    public static string SerializeToJson(AddressDetailV60Incorporated value)
    {
        var description = GetEnumDescription(value);
        return JsonSerializer.Serialize(description);
    }

    /// <summary>
    /// Serializes a AddressDetailV60Incorporated enum value to its string representation (Description value)
    /// </summary>
    public static string SerializeToString(AddressDetailV60Incorporated value)
    {
        return GetEnumDescription(value) ?? value.ToString();
    }
}
