using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Serializer for OriginDestinationV60 - handles serialization and deserialization
/// </summary>
public static class OriginDestinationV60Serializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a OriginDestinationV60 instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static OriginDestinationV60? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a OriginDestinationV60 instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static OriginDestinationV60? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static OriginDestinationV60 DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string adjustmentType = default!;
        string description = default!;
        OriginDestinationV60Value value = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "adjustmentType":
                    adjustmentType = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "description":
                    description = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "value":
                    var valueValue = OriginDestinationV60ValueSerializer.DeserializeFromElement(property.Value);
                    if (valueValue != null)
                        value = valueValue.Value;
                    break;
            }
        }

        // Create instance with all properties set at once
        return new OriginDestinationV60
        {
            AdjustmentType = adjustmentType!,
            Description = description!,
            Value = value!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of OriginDestinationV60 instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<OriginDestinationV60> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<OriginDestinationV60>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<OriginDestinationV60>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a OriginDestinationV60 instance to a JSON string
    /// </summary>
    public static string SerializeToJson(OriginDestinationV60 instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of OriginDestinationV60 instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<OriginDestinationV60> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
