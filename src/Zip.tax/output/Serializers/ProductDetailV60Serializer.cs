using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Serializer for ProductDetailV60 - handles serialization and deserialization
/// </summary>
public static class ProductDetailV60Serializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a ProductDetailV60 instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static ProductDetailV60? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a ProductDetailV60 instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static ProductDetailV60? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static ProductDetailV60 DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        TaxabilityCodeV60 taxabilityCode = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "taxabilityCode":
                    taxabilityCode = TaxabilityCodeV60Serializer.DeserializeFromElementCore(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new ProductDetailV60
        {
            TaxabilityCode = taxabilityCode!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of ProductDetailV60 instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<ProductDetailV60> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<ProductDetailV60>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<ProductDetailV60>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a ProductDetailV60 instance to a JSON string
    /// </summary>
    public static string SerializeToJson(ProductDetailV60 instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of ProductDetailV60 instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<ProductDetailV60> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
