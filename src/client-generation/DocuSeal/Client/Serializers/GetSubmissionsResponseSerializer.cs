using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmissionsResponse - handles serialization and deserialization
/// </summary>
public static class GetSubmissionsResponseSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmissionsResponse instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionsResponse? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmissionsResponse instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionsResponse? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmissionsResponse DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        List<GetSubmissionsResponseDataItem>? data = null;
        GetSubmissionsResponsePagination pagination = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "data":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        data = new List<GetSubmissionsResponseDataItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetSubmissionsResponseDataItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                data.Add(_itemValue);
                        }
                    }
                    break;
                case "pagination":
                    pagination = GetSubmissionsResponsePaginationSerializer.DeserializeFromElementCore(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetSubmissionsResponse
        {
            Data = data!,
            Pagination = pagination!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmissionsResponse instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmissionsResponse> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmissionsResponse>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmissionsResponse>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmissionsResponse instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmissionsResponse instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmissionsResponse instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmissionsResponse> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
