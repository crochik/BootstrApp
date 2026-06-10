using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for UpdateSubmitterRequestFieldsItem - handles serialization and deserialization
/// </summary>
public static class UpdateSubmitterRequestFieldsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a UpdateSubmitterRequestFieldsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static UpdateSubmitterRequestFieldsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a UpdateSubmitterRequestFieldsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static UpdateSubmitterRequestFieldsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static UpdateSubmitterRequestFieldsItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string name = default!;
        object? defaultValue = default;
        bool? _readonly = default;
        bool? required = default;
        UpdateSubmitterRequestFieldsItemValidation? validation = default;
        UpdateSubmitterRequestFieldsItemPreferences? preferences = default;

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
                case "validation":
                    validation = UpdateSubmitterRequestFieldsItemValidationSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "preferences":
                    preferences = UpdateSubmitterRequestFieldsItemPreferencesSerializer.DeserializeFromElementCore(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new UpdateSubmitterRequestFieldsItem
        {
            Name = name!,
            DefaultValue = defaultValue,
            Readonly = _readonly,
            Required = required,
            Validation = validation,
            Preferences = preferences
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of UpdateSubmitterRequestFieldsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<UpdateSubmitterRequestFieldsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<UpdateSubmitterRequestFieldsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<UpdateSubmitterRequestFieldsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a UpdateSubmitterRequestFieldsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(UpdateSubmitterRequestFieldsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of UpdateSubmitterRequestFieldsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<UpdateSubmitterRequestFieldsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
