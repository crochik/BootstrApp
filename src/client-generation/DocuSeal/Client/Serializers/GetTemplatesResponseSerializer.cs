using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetTemplatesResponse - handles serialization and deserialization
/// </summary>
public static class GetTemplatesResponseSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetTemplatesResponse instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetTemplatesResponse? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetTemplatesResponse instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetTemplatesResponse? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetTemplatesResponse DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        List<GetTemplatesResponseDataItem>? data = null;
        GetTemplatesResponsePagination pagination = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "data":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        data = new List<GetTemplatesResponseDataItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetTemplatesResponseDataItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                data.Add(_itemValue);
                        }
                    }
                    break;
                case "pagination":
                    pagination = GetTemplatesResponsePaginationSerializer.DeserializeFromElementCore(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetTemplatesResponse
        {
            Data = data!,
            Pagination = pagination!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetTemplatesResponse instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetTemplatesResponse> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetTemplatesResponse>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetTemplatesResponse>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetTemplatesResponse instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetTemplatesResponse instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetTemplatesResponse instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetTemplatesResponse> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
