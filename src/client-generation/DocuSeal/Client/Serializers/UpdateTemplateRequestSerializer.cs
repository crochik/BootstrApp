using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for UpdateTemplateRequest - handles serialization and deserialization
/// </summary>
public static class UpdateTemplateRequestSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a UpdateTemplateRequest instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static UpdateTemplateRequest? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a UpdateTemplateRequest instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static UpdateTemplateRequest? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static UpdateTemplateRequest DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        string? name = default;
        string? folderName = default;
        List<string>? roles = null;
        bool? archived = default;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "folder_name":
                    folderName = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "roles":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        roles = new List<string>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            roles.Add(BaseSerializer.ParsePrimitivestring(_item));
                        }
                    }
                    break;
                case "archived":
                    archived = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new UpdateTemplateRequest
        {
            Name = name,
            FolderName = folderName,
            Roles = roles,
            Archived = archived
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of UpdateTemplateRequest instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<UpdateTemplateRequest> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<UpdateTemplateRequest>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<UpdateTemplateRequest>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a UpdateTemplateRequest instance to a JSON string
    /// </summary>
    public static string SerializeToJson(UpdateTemplateRequest instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of UpdateTemplateRequest instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<UpdateTemplateRequest> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
