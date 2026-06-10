using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Serializer for ShippingV60 - handles serialization and deserialization
/// </summary>
public static class ShippingV60Serializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a ShippingV60 instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static ShippingV60? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a ShippingV60 instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static ShippingV60? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static ShippingV60 DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string adjustmentType = default!;
        ShippingV60Taxable taxable = default!;
        string description = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "adjustmentType":
                    adjustmentType = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "taxable":
                    var taxableValue = ShippingV60TaxableSerializer.DeserializeFromElement(property.Value);
                    if (taxableValue != null)
                        taxable = taxableValue.Value;
                    break;
                case "description":
                    description = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new ShippingV60
        {
            AdjustmentType = adjustmentType!,
            Taxable = taxable!,
            Description = description!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of ShippingV60 instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<ShippingV60> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<ShippingV60>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<ShippingV60>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a ShippingV60 instance to a JSON string
    /// </summary>
    public static string SerializeToJson(ShippingV60 instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of ShippingV60 instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<ShippingV60> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
