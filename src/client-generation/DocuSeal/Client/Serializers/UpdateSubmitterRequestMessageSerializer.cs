using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for UpdateSubmitterRequestMessage - handles serialization and deserialization
/// </summary>
public static class UpdateSubmitterRequestMessageSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a UpdateSubmitterRequestMessage instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static UpdateSubmitterRequestMessage? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a UpdateSubmitterRequestMessage instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static UpdateSubmitterRequestMessage? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static UpdateSubmitterRequestMessage DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string? subject = default;
        string? body = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "subject":
                    subject = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "body":
                    body = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new UpdateSubmitterRequestMessage
        {
            Subject = subject,
            Body = body
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of UpdateSubmitterRequestMessage instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<UpdateSubmitterRequestMessage> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<UpdateSubmitterRequestMessage>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<UpdateSubmitterRequestMessage>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a UpdateSubmitterRequestMessage instance to a JSON string
    /// </summary>
    public static string SerializeToJson(UpdateSubmitterRequestMessage instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of UpdateSubmitterRequestMessage instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<UpdateSubmitterRequestMessage> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
