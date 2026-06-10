using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromPdfResponseDocumentsItem - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromPdfResponseDocumentsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromPdfResponseDocumentsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromPdfResponseDocumentsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromPdfResponseDocumentsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromPdfResponseDocumentsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromPdfResponseDocumentsItem DeserializeFromElementCore(JsonElement element)
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
        return new CreateTemplateFromPdfResponseDocumentsItem
        {
            Id = id!,
            Uuid = uuid!,
            Url = url!,
            PreviewImageUrl = previewImageUrl!,
            Filename = filename!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateTemplateFromPdfResponseDocumentsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromPdfResponseDocumentsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromPdfResponseDocumentsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromPdfResponseDocumentsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromPdfResponseDocumentsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromPdfResponseDocumentsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromPdfResponseDocumentsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromPdfResponseDocumentsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
