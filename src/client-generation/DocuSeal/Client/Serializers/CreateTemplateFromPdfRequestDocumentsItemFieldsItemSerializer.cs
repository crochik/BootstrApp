using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromPdfRequestDocumentsItemFieldsItem - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromPdfRequestDocumentsItemFieldsItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromPdfRequestDocumentsItemFieldsItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromPdfRequestDocumentsItemFieldsItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromPdfRequestDocumentsItemFieldsItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromPdfRequestDocumentsItemFieldsItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromPdfRequestDocumentsItemFieldsItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string? name = default;
        string? type = default;
        string? role = default;
        bool? required = default;
        string? title = default;
        string? description = default;
        List<CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem>? areas = null;
        List<string>? options = null;
        CreateTemplateFromPdfRequestDocumentsItemFieldsItemValidation? validation = default;
        CreateTemplateFromPdfRequestDocumentsItemFieldsItemPreferences? preferences = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "type":
                    type = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "role":
                    role = BaseSerializer.ParsePrimitivestring(property.Value);
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
                case "areas":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        areas = new List<CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateTemplateFromPdfRequestDocumentsItemFieldsItemAreasItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                areas.Add(_itemValue);
                        }
                    }
                    break;
                case "options":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        options = new List<string>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            options.Add(BaseSerializer.ParsePrimitivestring(_item));
                        }
                    }
                    break;
                case "validation":
                    validation = CreateTemplateFromPdfRequestDocumentsItemFieldsItemValidationSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "preferences":
                    preferences = CreateTemplateFromPdfRequestDocumentsItemFieldsItemPreferencesSerializer.DeserializeFromElementCore(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateTemplateFromPdfRequestDocumentsItemFieldsItem
        {
            Name = name,
            Type = type,
            Role = role,
            Required = required,
            Title = title,
            Description = description,
            Areas = areas,
            Options = options,
            Validation = validation,
            Preferences = preferences
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateTemplateFromPdfRequestDocumentsItemFieldsItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromPdfRequestDocumentsItemFieldsItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromPdfRequestDocumentsItemFieldsItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromPdfRequestDocumentsItemFieldsItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromPdfRequestDocumentsItemFieldsItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromPdfRequestDocumentsItemFieldsItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromPdfRequestDocumentsItemFieldsItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromPdfRequestDocumentsItemFieldsItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
