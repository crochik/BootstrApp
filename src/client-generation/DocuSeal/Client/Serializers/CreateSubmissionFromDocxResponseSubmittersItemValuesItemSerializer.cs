using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionFromDocxResponseSubmittersItemValuesItem - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionFromDocxResponseSubmittersItemValuesItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionFromDocxResponseSubmittersItemValuesItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromDocxResponseSubmittersItemValuesItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionFromDocxResponseSubmittersItemValuesItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromDocxResponseSubmittersItemValuesItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionFromDocxResponseSubmittersItemValuesItem DeserializeFromElementCore(JsonElement element)
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
        return new CreateSubmissionFromDocxResponseSubmittersItemValuesItem
        {
            Field = field!,
            Value = value!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateSubmissionFromDocxResponseSubmittersItemValuesItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionFromDocxResponseSubmittersItemValuesItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionFromDocxResponseSubmittersItemValuesItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionFromDocxResponseSubmittersItemValuesItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionFromDocxResponseSubmittersItemValuesItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionFromDocxResponseSubmittersItemValuesItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionFromDocxResponseSubmittersItemValuesItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionFromDocxResponseSubmittersItemValuesItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
