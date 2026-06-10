using System.Text.Json;

namespace ZipTax.Models;

/// <summary>
/// Serializer for GetTaxRatesV60CountryCode - handles serialization and deserialization
/// </summary>
public static class GetTaxRatesV60CountryCodeSerializer
{
    /// <summary>
    /// Gets the Description attribute value for an enum member
    /// </summary>
    private static string? GetEnumDescription(GetTaxRatesV60CountryCode value)
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
    private static GetTaxRatesV60CountryCode? GetEnumFromDescription(string description)
    {
        var type = typeof(GetTaxRatesV60CountryCode);
        foreach (var field in type.GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute)) is System.ComponentModel.DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                    return (GetTaxRatesV60CountryCode)field.GetValue(null)!;
            }
        }

        // Fallback: try to parse by name
        return Enum.TryParse<GetTaxRatesV60CountryCode>(description, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetTaxRatesV60CountryCode enum value using Description attribute
    /// </summary>
    public static GetTaxRatesV60CountryCode? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        var stringValue = element.GetString();
        if (string.IsNullOrEmpty(stringValue))
            return null;

        return GetEnumFromDescription(stringValue);
    }

    /// <summary>
    /// Deserializes a JsonDocument into a GetTaxRatesV60CountryCode enum value
    /// </summary>
    public static GetTaxRatesV60CountryCode? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElement(document.RootElement);
    }

    /// <summary>
    /// Serializes a GetTaxRatesV60CountryCode enum value to a JSON string using Description attribute
    /// </summary>
    public static string SerializeToJson(GetTaxRatesV60CountryCode value)
    {
        var description = GetEnumDescription(value);
        return JsonSerializer.Serialize(description);
    }

    /// <summary>
    /// Serializes a GetTaxRatesV60CountryCode enum value to its string representation (Description value)
    /// </summary>
    public static string SerializeToString(GetTaxRatesV60CountryCode value)
    {
        return GetEnumDescription(value) ?? value.ToString();
    }
}
