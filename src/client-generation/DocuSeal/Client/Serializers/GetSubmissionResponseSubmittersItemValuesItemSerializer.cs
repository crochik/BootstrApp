using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmissionResponseSubmittersItemValuesItem - handles serialization and deserialization
/// </summary>
public static class GetSubmissionResponseSubmittersItemValuesItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmissionResponseSubmittersItemValuesItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionResponseSubmittersItemValuesItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmissionResponseSubmittersItemValuesItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionResponseSubmittersItemValuesItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmissionResponseSubmittersItemValuesItem DeserializeFromElementCore(JsonElement element)
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
        return new GetSubmissionResponseSubmittersItemValuesItem
        {
            Field = field!,
            Value = value!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmissionResponseSubmittersItemValuesItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmissionResponseSubmittersItemValuesItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmissionResponseSubmittersItemValuesItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmissionResponseSubmittersItemValuesItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmissionResponseSubmittersItemValuesItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmissionResponseSubmittersItemValuesItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmissionResponseSubmittersItemValuesItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmissionResponseSubmittersItemValuesItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
