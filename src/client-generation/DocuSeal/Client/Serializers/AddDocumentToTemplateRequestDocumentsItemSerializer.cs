using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for AddDocumentToTemplateRequestDocumentsItem - handles serialization and deserialization
/// </summary>
public static class AddDocumentToTemplateRequestDocumentsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a AddDocumentToTemplateRequestDocumentsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static AddDocumentToTemplateRequestDocumentsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a AddDocumentToTemplateRequestDocumentsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static AddDocumentToTemplateRequestDocumentsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static AddDocumentToTemplateRequestDocumentsItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string? name = default;
        string? file = default;
        string? html = default;
        int? position = default;
        bool? replace = default;
        bool? remove = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "file":
                    file = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "html":
                    html = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "position":
                    position = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "replace":
                    replace = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "remove":
                    remove = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new AddDocumentToTemplateRequestDocumentsItem
        {
            Name = name,
            File = file,
            Html = html,
            Position = position,
            Replace = replace,
            Remove = remove
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of AddDocumentToTemplateRequestDocumentsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<AddDocumentToTemplateRequestDocumentsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<AddDocumentToTemplateRequestDocumentsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<AddDocumentToTemplateRequestDocumentsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a AddDocumentToTemplateRequestDocumentsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(AddDocumentToTemplateRequestDocumentsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of AddDocumentToTemplateRequestDocumentsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<AddDocumentToTemplateRequestDocumentsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
