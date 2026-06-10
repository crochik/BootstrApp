using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipTax.Models;

/// <summary>
/// Serializer for ResponseInfoV60 - handles serialization and deserialization
/// </summary>
public static class ResponseInfoV60Serializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a ResponseInfoV60 instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static ResponseInfoV60? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a ResponseInfoV60 instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static ResponseInfoV60? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static ResponseInfoV60 DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int code = default!;
        string name = default!;
        string message = default!;
        string? definition = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "code":
                    code = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "message":
                    message = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "definition":
                    definition = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new ResponseInfoV60
        {
            Code = code!,
            Name = name!,
            Message = message!,
            Definition = definition
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of ResponseInfoV60 instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<ResponseInfoV60> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<ResponseInfoV60>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<ResponseInfoV60>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a ResponseInfoV60 instance to a JSON string
    /// </summary>
    public static string SerializeToJson(ResponseInfoV60 instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of ResponseInfoV60 instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<ResponseInfoV60> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
