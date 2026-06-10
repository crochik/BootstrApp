using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromHtmlRequest - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromHtmlRequestSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromHtmlRequest instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromHtmlRequest? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromHtmlRequest instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromHtmlRequest? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromHtmlRequest DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string html = default!;
        string? htmlHeader = default;
        string? htmlFooter = default;
        string? name = default;
        string? size = default;
        string? externalId = default;
        string? folderName = default;
        bool? sharedLink = default;
        List<CreateTemplateFromHtmlRequestDocumentsItem>? documents = null;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "html":
                    html = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "html_header":
                    htmlHeader = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "html_footer":
                    htmlFooter = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "size":
                    size = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "external_id":
                    externalId = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "folder_name":
                    folderName = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "shared_link":
                    sharedLink = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "documents":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        documents = new List<CreateTemplateFromHtmlRequestDocumentsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateTemplateFromHtmlRequestDocumentsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                documents.Add(_itemValue);
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateTemplateFromHtmlRequest
        {
            Html = html!,
            HtmlHeader = htmlHeader,
            HtmlFooter = htmlFooter,
            Name = name,
            Size = size,
            ExternalId = externalId,
            FolderName = folderName,
            SharedLink = sharedLink,
            Documents = documents
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateTemplateFromHtmlRequest instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromHtmlRequest> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromHtmlRequest>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromHtmlRequest>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromHtmlRequest instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromHtmlRequest instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromHtmlRequest instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromHtmlRequest> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
