using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmittersResponsePagination - handles serialization and deserialization
/// </summary>
public static class GetSubmittersResponsePaginationSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmittersResponsePagination instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmittersResponsePagination? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmittersResponsePagination instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmittersResponsePagination? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmittersResponsePagination DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int count = default!;
        int next = default!;
        int prev = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "count":
                    count = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "next":
                    next = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "prev":
                    prev = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetSubmittersResponsePagination
        {
            Count = count!,
            Next = next!,
            Prev = prev!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmittersResponsePagination instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmittersResponsePagination> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmittersResponsePagination>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmittersResponsePagination>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmittersResponsePagination instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmittersResponsePagination instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmittersResponsePagination instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmittersResponsePagination> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
