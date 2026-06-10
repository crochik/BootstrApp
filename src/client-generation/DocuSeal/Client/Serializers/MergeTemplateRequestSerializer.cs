using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for MergeTemplateRequest - handles serialization and deserialization
/// </summary>
public static class MergeTemplateRequestSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a MergeTemplateRequest instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static MergeTemplateRequest? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a MergeTemplateRequest instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static MergeTemplateRequest? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static MergeTemplateRequest DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        List<int>? templateIds = null;
        string? name = default;
        string? folderName = default;
        string? externalId = default;
        bool? sharedLink = default;
        List<string>? roles = null;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "template_ids":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        templateIds = new List<int>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            templateIds.Add(BaseSerializer.ParsePrimitiveint(_item));
                        }
                    }
                    break;
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "folder_name":
                    folderName = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "external_id":
                    externalId = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "shared_link":
                    sharedLink = BaseSerializer.ParsePrimitivebool(property.Value);
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
            }
        }

        // Create instance with all properties set at once
        return new MergeTemplateRequest
        {
            TemplateIds = templateIds!,
            Name = name,
            FolderName = folderName,
            ExternalId = externalId,
            SharedLink = sharedLink,
            Roles = roles
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of MergeTemplateRequest instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<MergeTemplateRequest> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<MergeTemplateRequest>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<MergeTemplateRequest>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a MergeTemplateRequest instance to a JSON string
    /// </summary>
    public static string SerializeToJson(MergeTemplateRequest instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of MergeTemplateRequest instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<MergeTemplateRequest> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
