using System.Text.Json;

namespace ZipTax.Models;

/// <summary>
/// Serializer for GetTaxRatesV60AddressDetailExtended - handles serialization and deserialization
/// </summary>
public static class GetTaxRatesV60AddressDetailExtendedSerializer
{
    /// <summary>
    /// Gets the Description attribute value for an enum member
    /// </summary>
    private static string? GetEnumDescription(GetTaxRatesV60AddressDetailExtended value)
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
    private static GetTaxRatesV60AddressDetailExtended? GetEnumFromDescription(string description)
    {
        var type = typeof(GetTaxRatesV60AddressDetailExtended);
        foreach (var field in type.GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute)) is System.ComponentModel.DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                    return (GetTaxRatesV60AddressDetailExtended)field.GetValue(null)!;
            }
        }

        // Fallback: try to parse by name
        return Enum.TryParse<GetTaxRatesV60AddressDetailExtended>(description, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetTaxRatesV60AddressDetailExtended enum value using Description attribute
    /// </summary>
    public static GetTaxRatesV60AddressDetailExtended? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        var stringValue = element.GetString();
        if (string.IsNullOrEmpty(stringValue))
            return null;

        return GetEnumFromDescription(stringValue);
    }

    /// <summary>
    /// Deserializes a JsonDocument into a GetTaxRatesV60AddressDetailExtended enum value
    /// </summary>
    public static GetTaxRatesV60AddressDetailExtended? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElement(document.RootElement);
    }

    /// <summary>
    /// Serializes a GetTaxRatesV60AddressDetailExtended enum value to a JSON string using Description attribute
    /// </summary>
    public static string SerializeToJson(GetTaxRatesV60AddressDetailExtended value)
    {
        var description = GetEnumDescription(value);
        return JsonSerializer.Serialize(description);
    }

    /// <summary>
    /// Serializes a GetTaxRatesV60AddressDetailExtended enum value to its string representation (Description value)
    /// </summary>
    public static string SerializeToString(GetTaxRatesV60AddressDetailExtended value)
    {
        return GetEnumDescription(value) ?? value.ToString();
    }
}
