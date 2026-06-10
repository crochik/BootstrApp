using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmissionsResponseDataItem - handles serialization and deserialization
/// </summary>
public static class GetSubmissionsResponseDataItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmissionsResponseDataItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionsResponseDataItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmissionsResponseDataItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionsResponseDataItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmissionsResponseDataItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        string? name = default;
        string source = default!;
        string slug = default!;
        string status = default!;
        string submittersOrder = default!;
        string auditLogUrl = default!;
        string? combinedDocumentUrl = default;
        string completedAt = default!;
        string createdAt = default!;
        string updatedAt = default!;
        string? archivedAt = default;
        List<GetSubmissionsResponseDataItemSubmittersItem>? submitters = null;
        GetSubmissionsResponseDataItemTemplate? template = default;
        GetSubmissionsResponseDataItemCreatedByUser createdByUser = default!;

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
                case "source":
                    source = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "slug":
                    slug = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "status":
                    status = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "submitters_order":
                    submittersOrder = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "audit_log_url":
                    auditLogUrl = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "combined_document_url":
                    combinedDocumentUrl = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "completed_at":
                    completedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "created_at":
                    createdAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "updated_at":
                    updatedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "archived_at":
                    archivedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "submitters":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        submitters = new List<GetSubmissionsResponseDataItemSubmittersItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetSubmissionsResponseDataItemSubmittersItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                submitters.Add(_itemValue);
                        }
                    }
                    break;
                case "template":
                    template = GetSubmissionsResponseDataItemTemplateSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "created_by_user":
                    createdByUser = GetSubmissionsResponseDataItemCreatedByUserSerializer.DeserializeFromElementCore(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetSubmissionsResponseDataItem
        {
            Id = id!,
            Name = name,
            Source = source!,
            Slug = slug!,
            Status = status!,
            SubmittersOrder = submittersOrder!,
            AuditLogUrl = auditLogUrl!,
            CombinedDocumentUrl = combinedDocumentUrl,
            CompletedAt = completedAt!,
            CreatedAt = createdAt!,
            UpdatedAt = updatedAt!,
            ArchivedAt = archivedAt,
            Submitters = submitters!,
            Template = template,
            CreatedByUser = createdByUser!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmissionsResponseDataItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmissionsResponseDataItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmissionsResponseDataItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmissionsResponseDataItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmissionsResponseDataItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmissionsResponseDataItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmissionsResponseDataItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmissionsResponseDataItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
