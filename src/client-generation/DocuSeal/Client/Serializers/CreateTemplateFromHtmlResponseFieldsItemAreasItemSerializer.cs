using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromHtmlResponseFieldsItemAreasItem - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromHtmlResponseFieldsItemAreasItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromHtmlResponseFieldsItemAreasItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromHtmlResponseFieldsItemAreasItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromHtmlResponseFieldsItemAreasItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromHtmlResponseFieldsItemAreasItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromHtmlResponseFieldsItemAreasItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        double x = default!;
        double y = default!;
        double w = default!;
        double h = default!;
        string attachmentUuid = default!;
        int page = default!;

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
                case "attachment_uuid":
                    attachmentUuid = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "page":
                    page = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateTemplateFromHtmlResponseFieldsItemAreasItem
        {
            X = x!,
            Y = y!,
            W = w!,
            H = h!,
            AttachmentUuid = attachmentUuid!,
            Page = page!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateTemplateFromHtmlResponseFieldsItemAreasItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromHtmlResponseFieldsItemAreasItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromHtmlResponseFieldsItemAreasItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromHtmlResponseFieldsItemAreasItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromHtmlResponseFieldsItemAreasItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromHtmlResponseFieldsItemAreasItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromHtmlResponseFieldsItemAreasItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromHtmlResponseFieldsItemAreasItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
