using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmissionsResponseDataItemTemplate - handles serialization and deserialization
/// </summary>
public static class GetSubmissionsResponseDataItemTemplateSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmissionsResponseDataItemTemplate instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionsResponseDataItemTemplate? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmissionsResponseDataItemTemplate instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionsResponseDataItemTemplate? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmissionsResponseDataItemTemplate DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        string name = default!;
        string externalId = default!;
        string folderName = default!;
        string createdAt = default!;
        string updatedAt = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    id = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "external_id":
                    externalId = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "folder_name":
                    folderName = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "created_at":
                    createdAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "updated_at":
                    updatedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetSubmissionsResponseDataItemTemplate
        {
            Id = id!,
            Name = name!,
            ExternalId = externalId!,
            FolderName = folderName!,
            CreatedAt = createdAt!,
            UpdatedAt = updatedAt!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmissionsResponseDataItemTemplate instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmissionsResponseDataItemTemplate> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmissionsResponseDataItemTemplate>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmissionsResponseDataItemTemplate>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmissionsResponseDataItemTemplate instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmissionsResponseDataItemTemplate instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmissionsResponseDataItemTemplate instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmissionsResponseDataItemTemplate> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
