using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmissionResponse - handles serialization and deserialization
/// </summary>
public static class GetSubmissionResponseSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmissionResponse instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionResponse? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmissionResponse instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionResponse? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmissionResponse DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        string? name = default;
        string slug = default!;
        string source = default!;
        string submittersOrder = default!;
        string auditLogUrl = default!;
        string combinedDocumentUrl = default!;
        string createdAt = default!;
        string updatedAt = default!;
        string archivedAt = default!;
        List<GetSubmissionResponseSubmittersItem>? submitters = null;
        GetSubmissionResponseTemplate? template = default;
        GetSubmissionResponseCreatedByUser createdByUser = default!;
        List<GetSubmissionResponseSubmissionEventsItem>? submissionEvents = null;
        List<GetSubmissionResponseDocumentsItem>? documents = null;
        string status = default!;
        object metadata = default!;
        string completedAt = default!;

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
                case "slug":
                    slug = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "source":
                    source = BaseSerializer.ParsePrimitivestring(property.Value);
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
                        submitters = new List<GetSubmissionResponseSubmittersItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetSubmissionResponseSubmittersItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                submitters.Add(_itemValue);
                        }
                    }
                    break;
                case "template":
                    template = GetSubmissionResponseTemplateSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "created_by_user":
                    createdByUser = GetSubmissionResponseCreatedByUserSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "submission_events":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        submissionEvents = new List<GetSubmissionResponseSubmissionEventsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetSubmissionResponseSubmissionEventsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                submissionEvents.Add(_itemValue);
                        }
                    }
                    break;
                case "documents":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        documents = new List<GetSubmissionResponseDocumentsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetSubmissionResponseDocumentsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                documents.Add(_itemValue);
                        }
                    }
                    break;
                case "status":
                    status = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "metadata":
                    metadata = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
                case "completed_at":
                    completedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetSubmissionResponse
        {
            Id = id!,
            Name = name,
            Slug = slug!,
            Source = source!,
            SubmittersOrder = submittersOrder!,
            AuditLogUrl = auditLogUrl!,
            CombinedDocumentUrl = combinedDocumentUrl!,
            CreatedAt = createdAt!,
            UpdatedAt = updatedAt!,
            ArchivedAt = archivedAt!,
            Submitters = submitters!,
            Template = template,
            CreatedByUser = createdByUser!,
            SubmissionEvents = submissionEvents!,
            Documents = documents!,
            Status = status!,
            Metadata = metadata!,
            CompletedAt = completedAt!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmissionResponse instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmissionResponse> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmissionResponse>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmissionResponse>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmissionResponse instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmissionResponse instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmissionResponse instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmissionResponse> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
