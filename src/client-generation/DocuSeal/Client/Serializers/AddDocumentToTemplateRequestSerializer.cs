using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for AddDocumentToTemplateRequest - handles serialization and deserialization
/// </summary>
public static class AddDocumentToTemplateRequestSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a AddDocumentToTemplateRequest instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static AddDocumentToTemplateRequest? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a AddDocumentToTemplateRequest instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static AddDocumentToTemplateRequest? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static AddDocumentToTemplateRequest DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        List<AddDocumentToTemplateRequestDocumentsItem>? documents = null;
        bool? merge = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "documents":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        documents = new List<AddDocumentToTemplateRequestDocumentsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = AddDocumentToTemplateRequestDocumentsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                documents.Add(_itemValue);
                        }
                    }
                    break;
                case "merge":
                    merge = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new AddDocumentToTemplateRequest
        {
            Documents = documents,
            Merge = merge
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of AddDocumentToTemplateRequest instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<AddDocumentToTemplateRequest> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<AddDocumentToTemplateRequest>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<AddDocumentToTemplateRequest>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a AddDocumentToTemplateRequest instance to a JSON string
    /// </summary>
    public static string SerializeToJson(AddDocumentToTemplateRequest instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of AddDocumentToTemplateRequest instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<AddDocumentToTemplateRequest> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
