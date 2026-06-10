using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromPdfRequest - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromPdfRequestSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromPdfRequest instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromPdfRequest? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromPdfRequest instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromPdfRequest? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromPdfRequest DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string? name = default;
        string? folderName = default;
        string? externalId = default;
        bool? sharedLink = default;
        List<CreateTemplateFromPdfRequestDocumentsItem>? documents = null;
        bool? flatten = default;
        bool? removeTags = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "folder_name":
                    folderName = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "external_id":
                    externalId = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "shared_link":
                    sharedLink = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "documents":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        documents = new List<CreateTemplateFromPdfRequestDocumentsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateTemplateFromPdfRequestDocumentsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                documents.Add(_itemValue);
                        }
                    }
                    break;
                case "flatten":
                    flatten = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "remove_tags":
                    removeTags = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateTemplateFromPdfRequest
        {
            Name = name,
            FolderName = folderName,
            ExternalId = externalId,
            SharedLink = sharedLink,
            Documents = documents!,
            Flatten = flatten,
            RemoveTags = removeTags
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateTemplateFromPdfRequest instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromPdfRequest> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromPdfRequest>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromPdfRequest>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromPdfRequest instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromPdfRequest instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromPdfRequest instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromPdfRequest> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
