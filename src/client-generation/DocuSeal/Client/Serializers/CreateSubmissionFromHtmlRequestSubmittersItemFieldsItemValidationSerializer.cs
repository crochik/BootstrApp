using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidationSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string? pattern = default;
        string? message = default;
        object? min = default;
        object? max = default;
        double? step = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "pattern":
                    pattern = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "message":
                    message = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "min":
                    min = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
                case "max":
                    max = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
                case "step":
                    step = BaseSerializer.ParsePrimitivedouble(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation
        {
            Pattern = pattern,
            Message = message,
            Min = min,
            Max = max,
            Step = step
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionFromHtmlRequestSubmittersItemFieldsItemValidation> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
