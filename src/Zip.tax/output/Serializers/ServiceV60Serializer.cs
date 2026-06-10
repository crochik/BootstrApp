using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Serializer for ServiceV60 - handles serialization and deserialization
/// </summary>
public static class ServiceV60Serializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a ServiceV60 instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static ServiceV60? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a ServiceV60 instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static ServiceV60? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static ServiceV60 DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string adjustmentType = default!;
        ServiceV60Taxable taxable = default!;
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
                    var taxableValue = ServiceV60TaxableSerializer.DeserializeFromElement(property.Value);
                    if (taxableValue != null)
                        taxable = taxableValue.Value;
                    break;
                case "description":
                    description = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new ServiceV60
        {
            AdjustmentType = adjustmentType!,
            Taxable = taxable!,
            Description = description!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of ServiceV60 instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<ServiceV60> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<ServiceV60>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<ServiceV60>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a ServiceV60 instance to a JSON string
    /// </summary>
    public static string SerializeToJson(ServiceV60 instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of ServiceV60 instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<ServiceV60> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
