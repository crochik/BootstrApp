using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Serializer for MetadataV60 - handles serialization and deserialization
/// </summary>
public static class MetadataV60Serializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a MetadataV60 instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static MetadataV60? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a MetadataV60 instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static MetadataV60? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static MetadataV60 DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string version = default!;
        ResponseInfoV60 response = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "version":
                    version = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "response":
                    response = ResponseInfoV60Serializer.DeserializeFromElementCore(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new MetadataV60
        {
            Version = version!,
            Response = response!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of MetadataV60 instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<MetadataV60> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<MetadataV60>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<MetadataV60>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a MetadataV60 instance to a JSON string
    /// </summary>
    public static string SerializeToJson(MetadataV60 instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of MetadataV60 instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<MetadataV60> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
