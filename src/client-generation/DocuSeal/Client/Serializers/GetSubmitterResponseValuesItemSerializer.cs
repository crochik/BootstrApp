using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmitterResponseValuesItem - handles serialization and deserialization
/// </summary>
public static class GetSubmitterResponseValuesItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmitterResponseValuesItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmitterResponseValuesItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmitterResponseValuesItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmitterResponseValuesItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmitterResponseValuesItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string field = default!;
        object value = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "field":
                    field = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "value":
                    value = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetSubmitterResponseValuesItem
        {
            Field = field!,
            Value = value!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmitterResponseValuesItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmitterResponseValuesItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmitterResponseValuesItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmitterResponseValuesItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmitterResponseValuesItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmitterResponseValuesItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmitterResponseValuesItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmitterResponseValuesItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
