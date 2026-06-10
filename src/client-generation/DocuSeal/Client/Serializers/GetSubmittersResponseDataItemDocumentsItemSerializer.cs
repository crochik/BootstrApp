using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmittersResponseDataItemDocumentsItem - handles serialization and deserialization
/// </summary>
public static class GetSubmittersResponseDataItemDocumentsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmittersResponseDataItemDocumentsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmittersResponseDataItemDocumentsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmittersResponseDataItemDocumentsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmittersResponseDataItemDocumentsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmittersResponseDataItemDocumentsItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string name = default!;
        string url = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "url":
                    url = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetSubmittersResponseDataItemDocumentsItem
        {
            Name = name!,
            Url = url!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmittersResponseDataItemDocumentsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmittersResponseDataItemDocumentsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmittersResponseDataItemDocumentsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmittersResponseDataItemDocumentsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmittersResponseDataItemDocumentsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmittersResponseDataItemDocumentsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmittersResponseDataItemDocumentsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmittersResponseDataItemDocumentsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
