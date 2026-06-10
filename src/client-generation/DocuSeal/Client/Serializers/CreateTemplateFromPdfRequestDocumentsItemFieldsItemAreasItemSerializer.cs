using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        double x = default!;
        double y = default!;
        double w = default!;
        double h = default!;
        int page = default!;
        string? option = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "x":
                    x = BaseSerializer.ParsePrimitivedouble(property.Value);
                    break;
                case "y":
                    y = BaseSerializer.ParsePrimitivedouble(property.Value);
                    break;
                case "w":
                    w = BaseSerializer.ParsePrimitivedouble(property.Value);
                    break;
                case "h":
                    h = BaseSerializer.ParsePrimitivedouble(property.Value);
                    break;
                case "page":
                    page = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "option":
                    option = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem
        {
            X = x!,
            Y = y!,
            W = w!,
            H = h!,
            Page = page!,
            Option = option
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
