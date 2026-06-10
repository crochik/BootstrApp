using System.Text.Json;

namespace ZipTax.Models;

/// <summary>
/// Serializer for ShippingV60Taxable - handles serialization and deserialization
/// </summary>
public static class ShippingV60TaxableSerializer
{
    /// <summary>
    /// Gets the Description attribute value for an enum member
    /// </summary>
    private static string? GetEnumDescription(ShippingV60Taxable value)
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
    private static ShippingV60Taxable? GetEnumFromDescription(string description)
    {
        var type = typeof(ShippingV60Taxable);
        foreach (var field in type.GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute)) is System.ComponentModel.DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                    return (ShippingV60Taxable)field.GetValue(null)!;
            }
        }

        // Fallback: try to parse by name
        return Enum.TryParse<ShippingV60Taxable>(description, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// Deserializes a JsonElement into a ShippingV60Taxable enum value using Description attribute
    /// </summary>
    public static ShippingV60Taxable? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        var stringValue = element.GetString();
        if (string.IsNullOrEmpty(stringValue))
            return null;

        return GetEnumFromDescription(stringValue);
    }

    /// <summary>
    /// Deserializes a JsonDocument into a ShippingV60Taxable enum value
    /// </summary>
    public static ShippingV60Taxable? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElement(document.RootElement);
    }

    /// <summary>
    /// Serializes a ShippingV60Taxable enum value to a JSON string using Description attribute
    /// </summary>
    public static string SerializeToJson(ShippingV60Taxable value)
    {
        var description = GetEnumDescription(value);
        return JsonSerializer.Serialize(description);
    }

    /// <summary>
    /// Serializes a ShippingV60Taxable enum value to its string representation (Description value)
    /// </summary>
    public static string SerializeToString(ShippingV60Taxable value)
    {
        return GetEnumDescription(value) ?? value.ToString();
    }
}
