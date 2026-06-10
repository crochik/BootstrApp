using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateSubmissionRequestSubmittersItemFieldsItem - handles serialization and deserialization
/// </summary>
public static class CreateSubmissionRequestSubmittersItemFieldsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateSubmissionRequestSubmittersItemFieldsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionRequestSubmittersItemFieldsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateSubmissionRequestSubmittersItemFieldsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateSubmissionRequestSubmittersItemFieldsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateSubmissionRequestSubmittersItemFieldsItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string name = default!;
        object? defaultValue = default;
        bool? _readonly = default;
        bool? required = default;
        string? title = default;
        string? description = default;
        CreateSubmissionRequestSubmittersItemFieldsItemValidation? validation = default;
        CreateSubmissionRequestSubmittersItemFieldsItemPreferences? preferences = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "default_value":
                    defaultValue = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
                case "readonly":
                    _readonly = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "required":
                    required = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "title":
                    title = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "description":
                    description = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "validation":
                    validation = CreateSubmissionRequestSubmittersItemFieldsItemValidationSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "preferences":
                    preferences = CreateSubmissionRequestSubmittersItemFieldsItemPreferencesSerializer.DeserializeFromElementCore(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateSubmissionRequestSubmittersItemFieldsItem
        {
            Name = name!,
            DefaultValue = defaultValue,
            Readonly = _readonly,
            Required = required,
            Title = title,
            Description = description,
            Validation = validation,
            Preferences = preferences
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateSubmissionRequestSubmittersItemFieldsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateSubmissionRequestSubmittersItemFieldsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateSubmissionRequestSubmittersItemFieldsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateSubmissionRequestSubmittersItemFieldsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateSubmissionRequestSubmittersItemFieldsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateSubmissionRequestSubmittersItemFieldsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateSubmissionRequestSubmittersItemFieldsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateSubmissionRequestSubmittersItemFieldsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
