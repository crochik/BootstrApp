using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionFromHtmlResponseSchemaItem - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionFromHtmlResponseSchemaItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionFromHtmlResponseSchemaItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromHtmlResponseSchemaItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionFromHtmlResponseSchemaItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromHtmlResponseSchemaItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionFromHtmlResponseSchemaItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string? attachmentUuid = default;
        string? name = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "attachment_uuid":
                    attachmentUuid = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateSubmissionFromHtmlResponseSchemaItem
        {
            AttachmentUuid = attachmentUuid,
            Name = name
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateSubmissionFromHtmlResponseSchemaItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionFromHtmlResponseSchemaItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionFromHtmlResponseSchemaItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionFromHtmlResponseSchemaItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionFromHtmlResponseSchemaItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionFromHtmlResponseSchemaItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionFromHtmlResponseSchemaItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionFromHtmlResponseSchemaItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
