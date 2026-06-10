using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for GetSubmissionResponseSubmittersItem - handles serialization and deserialization
/// </summary>
public static class GetSubmissionResponseSubmittersItemSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a GetSubmissionResponseSubmittersItem instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionResponseSubmittersItem? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a GetSubmissionResponseSubmittersItem instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static GetSubmissionResponseSubmittersItem? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static GetSubmissionResponseSubmittersItem DeserializeFromElementCore(JsonElement element)
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
        string externalId = default!;
        string status = default!;
        List<GetSubmissionResponseSubmittersItemValuesItem>? values = null;
        List<GetSubmissionResponseSubmittersItemDocumentsItem>? documents = null;
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
                case "external_id":
                    externalId = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "status":
                    status = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "values":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        values = new List<GetSubmissionResponseSubmittersItemValuesItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetSubmissionResponseSubmittersItemValuesItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                values.Add(_itemValue);
                        }
                    }
                    break;
                case "documents":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        documents = new List<GetSubmissionResponseSubmittersItemDocumentsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = GetSubmissionResponseSubmittersItemDocumentsItemSerializer.DeserializeFromElementCore(_item);
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
        return new GetSubmissionResponseSubmittersItem
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
            ExternalId = externalId!,
            Status = status!,
            Values = values!,
            Documents = documents!,
            Role = role!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of GetSubmissionResponseSubmittersItem instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<GetSubmissionResponseSubmittersItem> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<GetSubmissionResponseSubmittersItem>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<GetSubmissionResponseSubmittersItem>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a GetSubmissionResponseSubmittersItem instance to a JSON string
    /// </summary>
    public static string SerializeToJson(GetSubmissionResponseSubmittersItem instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of GetSubmissionResponseSubmittersItem instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<GetSubmissionResponseSubmittersItem> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
