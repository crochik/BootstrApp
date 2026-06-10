using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionFromHtmlRequestMessage - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionFromHtmlRequestMessageSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionFromHtmlRequestMessage instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromHtmlRequestMessage? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionFromHtmlRequestMessage instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromHtmlRequestMessage? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionFromHtmlRequestMessage DeserializeFromElementCore(JsonElement element)
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
        return new CreateSubmissionFromHtmlRequestMessage
        {
            Subject = subject,
            Body = body
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateSubmissionFromHtmlRequestMessage instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionFromHtmlRequestMessage> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionFromHtmlRequestMessage>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionFromHtmlRequestMessage>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionFromHtmlRequestMessage instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionFromHtmlRequestMessage instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionFromHtmlRequestMessage instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionFromHtmlRequestMessage> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
