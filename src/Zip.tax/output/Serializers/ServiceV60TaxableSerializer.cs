using System.Text.Json;

namespace ZipTax.Models;

/// <summary>
/// Serializer for ServiceV60Taxable - handles serialization and deserialization
/// </summary>
public static class ServiceV60TaxableSerializer
{
    /// <summary>
    /// Gets the Description attribute value for an enum member
    /// </summary>
    private static string? GetEnumDescription(ServiceV60Taxable value)
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
    private static ServiceV60Taxable? GetEnumFromDescription(string description)
    {
        var type = typeof(ServiceV60Taxable);
        foreach (var field in type.GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute)) is System.ComponentModel.DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                    return (ServiceV60Taxable)field.GetValue(null)!;
            }
        }

        // Fallback: try to parse by name
        return Enum.TryParse<ServiceV60Taxable>(description, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// Deserializes a JsonElement into a ServiceV60Taxable enum value using Description attribute
    /// </summary>
    public static ServiceV60Taxable? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        var stringValue = element.GetString();
        if (string.IsNullOrEmpty(stringValue))
            return null;

        return GetEnumFromDescription(stringValue);
    }

    /// <summary>
    /// Deserializes a JsonDocument into a ServiceV60Taxable enum value
    /// </summary>
    public static ServiceV60Taxable? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElement(document.RootElement);
    }

    /// <summary>
    /// Serializes a ServiceV60Taxable enum value to a JSON string using Description attribute
    /// </summary>
    public static string SerializeToJson(ServiceV60Taxable value)
    {
        var description = GetEnumDescription(value);
        return JsonSerializer.Serialize(description);
    }

    /// <summary>
    /// Serializes a ServiceV60Taxable enum value to its string representation (Description value)
    /// </summary>
    public static string SerializeToString(ServiceV60Taxable value)
    {
        return GetEnumDescription(value) ?? value.ToString();
    }
}
