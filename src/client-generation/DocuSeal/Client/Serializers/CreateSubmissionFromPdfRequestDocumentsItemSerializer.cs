using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionFromPdfRequestDocumentsItem - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionFromPdfRequestDocumentsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionFromPdfRequestDocumentsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromPdfRequestDocumentsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionFromPdfRequestDocumentsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromPdfRequestDocumentsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionFromPdfRequestDocumentsItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string name = default!;
        string file = default!;
        List<CreateSubmissionFromPdfRequestDocumentsItemFieldsItem>? fields = null;
        int? position = default;

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
                case "fields":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        fields = new List<CreateSubmissionFromPdfRequestDocumentsItemFieldsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateSubmissionFromPdfRequestDocumentsItemFieldsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                fields.Add(_itemValue);
                        }
                    }
                    break;
                case "position":
                    position = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateSubmissionFromPdfRequestDocumentsItem
        {
            Name = name!,
            File = file!,
            Fields = fields,
            Position = position
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateSubmissionFromPdfRequestDocumentsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionFromPdfRequestDocumentsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionFromPdfRequestDocumentsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionFromPdfRequestDocumentsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionFromPdfRequestDocumentsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionFromPdfRequestDocumentsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionFromPdfRequestDocumentsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionFromPdfRequestDocumentsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
