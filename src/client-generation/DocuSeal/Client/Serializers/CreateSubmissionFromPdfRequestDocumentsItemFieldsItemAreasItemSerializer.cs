using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem DeserializeFromElementCore(JsonElement element)
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
        return new CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem
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
    /// Deserializes a JsonDocument into a list of CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionFromPdfRequestDocumentsItemFieldsItemAreasItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
