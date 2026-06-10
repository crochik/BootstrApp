using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromHtmlResponseDocumentsItem - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromHtmlResponseDocumentsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromHtmlResponseDocumentsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromHtmlResponseDocumentsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromHtmlResponseDocumentsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromHtmlResponseDocumentsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromHtmlResponseDocumentsItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        string uuid = default!;
        string url = default!;
        string previewImageUrl = default!;
        string filename = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    id = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "uuid":
                    uuid = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "url":
                    url = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "preview_image_url":
                    previewImageUrl = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "filename":
                    filename = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateTemplateFromHtmlResponseDocumentsItem
        {
            Id = id!,
            Uuid = uuid!,
            Url = url!,
            PreviewImageUrl = previewImageUrl!,
            Filename = filename!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateTemplateFromHtmlResponseDocumentsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromHtmlResponseDocumentsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromHtmlResponseDocumentsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromHtmlResponseDocumentsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromHtmlResponseDocumentsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromHtmlResponseDocumentsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromHtmlResponseDocumentsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromHtmlResponseDocumentsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
