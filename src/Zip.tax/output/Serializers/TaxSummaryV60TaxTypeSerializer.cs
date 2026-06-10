using System.Text.Json;

namespace ZipTax.Models;

/// <summary>
/// Serializer for TaxSummaryV60TaxType - handles serialization and deserialization
/// </summary>
public static class TaxSummaryV60TaxTypeSerializer
{
    /// <summary>
    /// Gets the Description attribute value for an enum member
    /// </summary>
    private static string? GetEnumDescription(TaxSummaryV60TaxType value)
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
    private static TaxSummaryV60TaxType? GetEnumFromDescription(string description)
    {
        var type = typeof(TaxSummaryV60TaxType);
        foreach (var field in type.GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute)) is System.ComponentModel.DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                    return (TaxSummaryV60TaxType)field.GetValue(null)!;
            }
        }

        // Fallback: try to parse by name
        return Enum.TryParse<TaxSummaryV60TaxType>(description, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// Deserializes a JsonElement into a TaxSummaryV60TaxType enum value using Description attribute
    /// </summary>
    public static TaxSummaryV60TaxType? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        var stringValue = element.GetString();
        if (string.IsNullOrEmpty(stringValue))
            return null;

        return GetEnumFromDescription(stringValue);
    }

    /// <summary>
    /// Deserializes a JsonDocument into a TaxSummaryV60TaxType enum value
    /// </summary>
    public static TaxSummaryV60TaxType? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElement(document.RootElement);
    }

    /// <summary>
    /// Serializes a TaxSummaryV60TaxType enum value to a JSON string using Description attribute
    /// </summary>
    public static string SerializeToJson(TaxSummaryV60TaxType value)
    {
        var description = GetEnumDescription(value);
        return JsonSerializer.Serialize(description);
    }

    /// <summary>
    /// Serializes a TaxSummaryV60TaxType enum value to its string representation (Description value)
    /// </summary>
    public static string SerializeToString(TaxSummaryV60TaxType value)
    {
        return GetEnumDescription(value) ?? value.ToString();
    }
}
