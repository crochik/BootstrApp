using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmitterResponseTemplate - handles serialization and deserialization
/// </summary>
public static class GetSubmitterResponseTemplateSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmitterResponseTemplate instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmitterResponseTemplate? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmitterResponseTemplate instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmitterResponseTemplate? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmitterResponseTemplate DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        double id = default!;
        string name = default!;
        DateTimeOffset createdAt = default!;
        DateTimeOffset updatedAt = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    id = BaseSerializer.ParsePrimitivedouble(property.Value);
                    break;
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "created_at":
                    createdAt = BaseSerializer.ParsePrimitiveDateTimeOffset(property.Value);
                    break;
                case "updated_at":
                    updatedAt = BaseSerializer.ParsePrimitiveDateTimeOffset(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetSubmitterResponseTemplate
        {
            Id = id!,
            Name = name!,
            CreatedAt = createdAt!,
            UpdatedAt = updatedAt!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmitterResponseTemplate instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmitterResponseTemplate> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmitterResponseTemplate>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmitterResponseTemplate>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmitterResponseTemplate instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmitterResponseTemplate instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmitterResponseTemplate instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmitterResponseTemplate> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
