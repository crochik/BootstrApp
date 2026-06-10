using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromHtmlResponseAuthor - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromHtmlResponseAuthorSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromHtmlResponseAuthor instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromHtmlResponseAuthor? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromHtmlResponseAuthor instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromHtmlResponseAuthor? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromHtmlResponseAuthor DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        string firstName = default!;
        string lastName = default!;
        string email = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    id = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "first_name":
                    firstName = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "last_name":
                    lastName = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "email":
                    email = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateTemplateFromHtmlResponseAuthor
        {
            Id = id!,
            FirstName = firstName!,
            LastName = lastName!,
            Email = email!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CreateTemplateFromHtmlResponseAuthor instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromHtmlResponseAuthor> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromHtmlResponseAuthor>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromHtmlResponseAuthor>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromHtmlResponseAuthor instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromHtmlResponseAuthor instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromHtmlResponseAuthor instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromHtmlResponseAuthor> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
