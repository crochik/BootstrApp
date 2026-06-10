using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmissionDocumentsResponse - handles serialization and deserialization
/// </summary>
public static class GetSubmissionDocumentsResponseSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmissionDocumentsResponse instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionDocumentsResponse? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmissionDocumentsResponse instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionDocumentsResponse? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmissionDocumentsResponse DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        List<GetSubmissionDocumentsResponseDocumentsItem>? documents = null;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    id = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "documents":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        documents = new List<GetSubmissionDocumentsResponseDocumentsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetSubmissionDocumentsResponseDocumentsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                documents.Add(_itemValue);
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetSubmissionDocumentsResponse
        {
            Id = id!,
            Documents = documents!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmissionDocumentsResponse instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmissionDocumentsResponse> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmissionDocumentsResponse>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmissionDocumentsResponse>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmissionDocumentsResponse instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmissionDocumentsResponse instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmissionDocumentsResponse instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmissionDocumentsResponse> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
