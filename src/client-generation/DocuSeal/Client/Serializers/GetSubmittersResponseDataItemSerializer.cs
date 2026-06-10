using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmittersResponseDataItem - handles serialization and deserialization
/// </summary>
public static class GetSubmittersResponseDataItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmittersResponseDataItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmittersResponseDataItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmittersResponseDataItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmittersResponseDataItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmittersResponseDataItem DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        int submissionId = default!;
        string uuid = default!;
        string email = default!;
        string slug = default!;
        string sentAt = default!;
        string openedAt = default!;
        string completedAt = default!;
        string declinedAt = default!;
        string createdAt = default!;
        string updatedAt = default!;
        string name = default!;
        string phone = default!;
        string status = default!;
        string externalId = default!;
        object preferences = default!;
        object metadata = default!;
        List<GetSubmittersResponseDataItemSubmissionEventsItem>? submissionEvents = null;
        List<GetSubmittersResponseDataItemValuesItem>? values = null;
        List<GetSubmittersResponseDataItemDocumentsItem>? documents = null;
        string role = default!;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    id = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "submission_id":
                    submissionId = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "uuid":
                    uuid = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "email":
                    email = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "slug":
                    slug = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "sent_at":
                    sentAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "opened_at":
                    openedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "completed_at":
                    completedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "declined_at":
                    declinedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "created_at":
                    createdAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "updated_at":
                    updatedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "phone":
                    phone = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "status":
                    status = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "external_id":
                    externalId = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "preferences":
                    preferences = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
                case "metadata":
                    metadata = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
                case "submission_events":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        submissionEvents = new List<GetSubmittersResponseDataItemSubmissionEventsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetSubmittersResponseDataItemSubmissionEventsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                submissionEvents.Add(_itemValue);
                        }
                    }
                    break;
                case "values":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        values = new List<GetSubmittersResponseDataItemValuesItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetSubmittersResponseDataItemValuesItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                values.Add(_itemValue);
                        }
                    }
                    break;
                case "documents":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        documents = new List<GetSubmittersResponseDataItemDocumentsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetSubmittersResponseDataItemDocumentsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                documents.Add(_itemValue);
                        }
                    }
                    break;
                case "role":
                    role = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
            }
        }

        // Create instance with all properties set at once
        return new GetSubmittersResponseDataItem
        {
            Id = id!,
            SubmissionId = submissionId!,
            Uuid = uuid!,
            Email = email!,
            Slug = slug!,
            SentAt = sentAt!,
            OpenedAt = openedAt!,
            CompletedAt = completedAt!,
            DeclinedAt = declinedAt!,
            CreatedAt = createdAt!,
            UpdatedAt = updatedAt!,
            Name = name!,
            Phone = phone!,
            Status = status!,
            ExternalId = externalId!,
            Preferences = preferences!,
            Metadata = metadata!,
            SubmissionEvents = submissionEvents!,
            Values = values!,
            Documents = documents!,
            Role = role!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmittersResponseDataItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmittersResponseDataItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmittersResponseDataItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmittersResponseDataItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmittersResponseDataItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmittersResponseDataItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmittersResponseDataItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmittersResponseDataItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
